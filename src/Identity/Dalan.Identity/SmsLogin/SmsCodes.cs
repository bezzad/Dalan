using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dalan.Identity.Data;
using Dalan.Identity.Sessions;
using Dalan.Notification.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Identity.SmsLogin;

internal enum SmsOutcome { Sent, Wait, Limit, Unavailable }

internal enum CodeCheck { Right, Wrong }

/// <summary>
/// AUTH-11, AUTH-12, SEC-07, SEC-08: makes, limits and checks SMS login codes.
/// The rows of identity.sms_codes of the last 24 hours are the counters, so the limits survive a restart.
/// </summary>
internal sealed class SmsCodes(
    IDbContextFactory<IdentityDb> dbFactory,
    IOptionsMonitor<IdentityOptions> options,
    TimeProvider time,
    SessionKey key,
    SecurityLog log)
{
    private readonly byte[] _key = key.Derive("sms-code");

    /// <summary>Sends a new code when the limits allow it. With <see cref="SmsOutcome.Wait"/>, the time to wait is set.</summary>
    public async Task<(SmsOutcome Outcome, TimeSpan Wait)> SendAsync(
        string phone, string ip, ISmsSender sender, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        var now = time.GetUtcNow();
        var code = RandomNumberGenerator.GetInt32((int)Math.Pow(10, settings.SmsCodeDigits))
            .ToString(CultureInfo.InvariantCulture).PadLeft(settings.SmsCodeDigits, '0');
        var row = new SmsCode { Phone = phone, CodeHash = Hash(phone, code), Ip = ip, CreatedAt = now };

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            // Two calls for one number wait for each other here, so "count, then insert" is safe.
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({phone}, 0))", cancellationToken);
            var dayAgo = now.AddDays(-1);
            await db.SmsCodes.Where(c => c.CreatedAt <= dayAgo).ExecuteDeleteAsync(cancellationToken);

            var sentToPhone = await db.SmsCodes.Where(c => c.Phone == phone).Select(c => c.CreatedAt).ToListAsync(cancellationToken);
            if (sentToPhone.Count > 0 && sentToPhone.Max().AddSeconds(settings.SmsWaitSeconds) - now is var wait && wait > TimeSpan.Zero)
            {
                log.SmsRefused(phone, "too soon");
                return (SmsOutcome.Wait, wait);
            }

            if (sentToPhone.Count >= settings.SmsPerPhonePerDay
                || await db.SmsCodes.CountAsync(c => c.Ip == ip, cancellationToken) >= settings.SmsPerIpPerDay)
            {
                log.SmsRefused(phone, "daily limit");
                return (SmsOutcome.Limit, default);
            }

            if (await db.SmsCodes.CountAsync(cancellationToken) >= settings.SmsPerDay)
            {
                log.SmsBudgetUsedUp(settings.SmsPerDay);
                return (SmsOutcome.Unavailable, default);
            }

            db.SmsCodes.Add(row);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        if (await sender.SendLoginCodeAsync(phone, code, cancellationToken))
        {
            log.SmsSent(phone);
            return (SmsOutcome.Sent, default);
        }

        // A failed send costs the person nothing: the slot is free again.
        await db.SmsCodes.Where(c => c.Id == row.Id).ExecuteDeleteAsync(cancellationToken);
        log.SmsRefused(phone, "the sender failed");
        return (SmsOutcome.Unavailable, default);
    }

    /// <summary>
    /// Counts one try on the newest code of the number and says if the code is right (AUTH-12).
    /// A right code is not used up yet: call <see cref="UseAsync"/> or <see cref="GiveBackTryAsync"/> next.
    /// </summary>
    public async Task<(CodeCheck Check, long CodeId)> CheckAsync(string phone, string code, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        var oldest = time.GetUtcNow().AddSeconds(-settings.SmsCodeSeconds);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var newest = await db.SmsCodes.Where(c => c.Phone == phone).OrderByDescending(c => c.Id).FirstOrDefaultAsync(cancellationToken);
        if (newest is null || newest.Used || newest.CreatedAt <= oldest) return (CodeCheck.Wrong, 0);

        var counted = await db.SmsCodes
            .Where(c => c.Id == newest.Id && !c.Used && c.Tries < settings.SmsCodeTries)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.Tries, c => c.Tries + 1), cancellationToken);

        return counted == 1 && CryptographicOperations.FixedTimeEquals(newest.CodeHash, Hash(phone, code))
            ? (CodeCheck.Right, newest.Id)
            : (CodeCheck.Wrong, 0);
    }

    /// <summary>Marks a right code as used. False when another call used it first.</summary>
    public async Task<bool> UseAsync(long codeId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.SmsCodes.Where(c => c.Id == codeId && !c.Used)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.Used, true), cancellationToken) == 1;
    }

    /// <summary>The code was right but the call cannot finish yet (the name is missing): that try does not count.</summary>
    public async Task GiveBackTryAsync(long codeId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.SmsCodes.Where(c => c.Id == codeId)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.Tries, c => c.Tries - 1), cancellationToken);
    }

    /// <summary>A code is short, so a plain hash could be guessed from a stolen table. The key makes that useless.</summary>
    private byte[] Hash(string phone, string code) => HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes($"{phone}:{code}"));
}
