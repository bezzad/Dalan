using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dalan.Identity.Data;
using Dalan.Identity.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Identity.Codes;

internal enum CodeOutcome { Sent, Wait, Limit, Unavailable }

internal enum CodeCheck { Right, Wrong }

/// <summary>The limits of one channel. Read from the settings on every call, so a change works at once.</summary>
internal sealed record CodeRules(
    int Digits, int Seconds, int Tries, int WaitSeconds, int PerTargetPerDay, int LongWaitSeconds, int PerIpPerDay, int PerDay,
    string PerDaySetting);

/// <summary>
/// AUTH-11, AUTH-12, AUTH-24, AUTH-25, SEC-07, SEC-08, SEC-13: makes, limits and checks login codes of one channel.
/// The rows of the last 24 hours are the counters, so the limits survive a restart.
/// </summary>
internal abstract class LoginCodes<T>(
    string channel, IDbContextFactory<IdentityDb> dbFactory, TimeProvider time, SessionKey key, SecurityLog log) where T : LoginCode, new()
{
    private readonly byte[] _key = key.Derive($"{channel}-code");

    protected abstract CodeRules Rules { get; }

    /// <summary>
    /// Sends a new code when the limits allow it. Wait is the time left with <see cref="CodeOutcome.Wait"/> and
    /// <see cref="CodeOutcome.Limit"/>, and the time before the next code with <see cref="CodeOutcome.Sent"/>.
    /// SEC-07: after <c>PerTargetPerDay</c> codes in 24 hours the wait is long; <paramref name="noCap"/> (an admin) never has it.
    /// </summary>
    public async Task<(CodeOutcome Outcome, TimeSpan Wait)> SendAsync(
        string target, string ip, Func<string, CancellationToken, Task<bool>> send, CancellationToken cancellationToken,
        bool noCap = false)
    {
        var rules = Rules;
        var now = time.GetUtcNow();
        // The wait after a number of codes to the target in 24 hours: short, then long after the cap (SEC-07, SEC-13).
        TimeSpan WaitAfter(int sent) =>
            TimeSpan.FromSeconds(sent >= rules.PerTargetPerDay && !noCap ? rules.LongWaitSeconds : rules.WaitSeconds);
        var code = RandomNumberGenerator.GetInt32((int)Math.Pow(10, rules.Digits))
            .ToString(CultureInfo.InvariantCulture).PadLeft(rules.Digits, '0');
        var row = new T { Target = target, CodeHash = Hash(target, code), Ip = ip, CreatedAt = now };

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var codes = db.Set<T>();
        TimeSpan next;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            // Two calls for one target wait for each other here, so "count, then insert" is safe.
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({target}, 0))", cancellationToken);
            var dayAgo = now.AddDays(-1);
            await codes.Where(c => c.CreatedAt <= dayAgo).ExecuteDeleteAsync(cancellationToken);

            var sentToTarget = await codes.CountAsync(c => c.Target == target, cancellationToken);
            if (sentToTarget > 0
                && await codes.Where(c => c.Target == target).MaxAsync(c => c.CreatedAt, cancellationToken)
                    + WaitAfter(sentToTarget) - now is var wait && wait > TimeSpan.Zero)
            {
                log.CodeRefused(channel, target, "too soon");
                return (CodeOutcome.Wait, wait);
            }

            var fromIp = codes.Where(c => c.Ip == ip);
            if (await fromIp.CountAsync(cancellationToken) >= rules.PerIpPerDay)
            {
                log.CodeRefused(channel, target, "daily limit of the address");
                return (CodeOutcome.Limit, await fromIp.MinAsync(c => c.CreatedAt, cancellationToken) + TimeSpan.FromDays(1) - now);
            }

            if (await codes.CountAsync(cancellationToken) >= rules.PerDay)
            {
                log.BudgetUsedUp(channel, rules.PerDay, rules.PerDaySetting);
                return (CodeOutcome.Unavailable, default);
            }

            codes.Add(row);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            next = WaitAfter(sentToTarget + 1);
        }

        if (await send(code, cancellationToken))
        {
            log.CodeSent(channel, target);
            return (CodeOutcome.Sent, next);
        }

        // A failed send costs the person nothing: the slot is free again.
        await codes.Where(c => c.Id == row.Id).ExecuteDeleteAsync(cancellationToken);
        log.CodeRefused(channel, target, "the sender failed");
        return (CodeOutcome.Unavailable, default);
    }

    /// <summary>
    /// Counts one try on the newest code of the target and says if the code is right (AUTH-12).
    /// A right code is not used up yet: call <see cref="UseAsync"/> or <see cref="GiveBackTryAsync"/> next.
    /// </summary>
    public async Task<(CodeCheck Check, long CodeId)> CheckAsync(string target, string code, CancellationToken cancellationToken)
    {
        var rules = Rules;
        var oldest = time.GetUtcNow().AddSeconds(-rules.Seconds);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var codes = db.Set<T>();
        var newest = await codes.Where(c => c.Target == target).OrderByDescending(c => c.Id).FirstOrDefaultAsync(cancellationToken);
        if (newest is null || newest.Used || newest.CreatedAt <= oldest) return (CodeCheck.Wrong, 0);

        var counted = await codes
            .Where(c => c.Id == newest.Id && !c.Used && c.Tries < rules.Tries)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.Tries, c => c.Tries + 1), cancellationToken);

        return counted == 1 && CryptographicOperations.FixedTimeEquals(newest.CodeHash, Hash(target, code))
            ? (CodeCheck.Right, newest.Id)
            : (CodeCheck.Wrong, 0);
    }

    /// <summary>Marks a right code as used. False when another call used it first.</summary>
    public async Task<bool> UseAsync(long codeId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<T>().Where(c => c.Id == codeId && !c.Used)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.Used, true), cancellationToken) == 1;
    }

    /// <summary>The code was right but the call cannot finish yet (the name is missing): that try does not count.</summary>
    public async Task GiveBackTryAsync(long codeId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Set<T>().Where(c => c.Id == codeId)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.Tries, c => c.Tries - 1), cancellationToken);
    }

    /// <summary>A code is short, so a plain hash could be guessed from a stolen table. The key makes that useless.</summary>
    private byte[] Hash(string target, string code) => HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes($"{target}:{code}"));
}

/// <summary>SMS codes (AUTH-11, AUTH-12, SEC-07, SEC-08).</summary>
internal sealed class SmsCodes(
    IDbContextFactory<IdentityDb> dbFactory, IOptionsMonitor<IdentityOptions> options, TimeProvider time, SessionKey key, SecurityLog log)
    : LoginCodes<SmsCode>("sms", dbFactory, time, key, log)
{
    protected override CodeRules Rules
    {
        get
        {
            var o = options.CurrentValue;
            return new(o.SmsCodeDigits, o.SmsCodeSeconds, o.SmsCodeTries, o.SmsWaitSeconds, o.SmsPerPhonePerDay, o.SmsLongWaitSeconds, o.SmsPerIpPerDay,
                o.SmsPerDay, "Identity:SmsPerDay");
        }
    }
}

/// <summary>Email codes (AUTH-24, AUTH-25, SEC-13).</summary>
internal sealed class EmailCodes(
    IDbContextFactory<IdentityDb> dbFactory, IOptionsMonitor<IdentityOptions> options, TimeProvider time, SessionKey key, SecurityLog log)
    : LoginCodes<EmailCode>("email", dbFactory, time, key, log)
{
    protected override CodeRules Rules
    {
        get
        {
            var o = options.CurrentValue;
            return new(o.EmailCodeDigits, o.EmailCodeSeconds, o.EmailCodeTries, o.EmailWaitSeconds, o.EmailPerAddressPerDay,
                o.EmailLongWaitSeconds, o.EmailPerIpPerDay, o.EmailPerDay, "Identity:EmailPerDay");
        }
    }
}
