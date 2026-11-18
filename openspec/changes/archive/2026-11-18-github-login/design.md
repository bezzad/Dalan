# Design

## Decisions

1. **OAuth web flow, no package.** Three plain HTTP calls with `HttpClient`: code to token, `GET /user`, `GET /user/emails`. Scope `user:email` only. The token is used once and never stored.
2. **A verified email is needed.** GitHub's primary verified email is the user's email. Without one the login fails with `github_no_email`. So every user still has a phone or an email (AUTH-01), and the legal record has an address to show.
3. **Same email = same user.** GitHub verified the address, like our email code does, so a GitHub login with the email of an existing user logs into that user and saves the GitHub id on it. No merge screen.
4. **State in a cookie.** A random state goes in `__Host-dalan_github` (`SameSite=Lax`, because the way back from GitHub is a cross-site navigation). The callback compares it in fixed time and deletes the cookie. No table.
5. **Callback URL is a setting**, not built from the request: the server behind Caddy sees `http`, and a setting cannot be changed by a `Host` header. `install.sh` writes it from the site domain.
6. **Errors go back to the page** as `/?login_error=<code>`, because the callback is a navigation, not a JSON call. The client shows the text and cleans the address.
7. **Level 1.** A GitHub user has no phone, so it is level 1 like an email user (AUTH-26 to build).
8. **Panel:** only `StateSeconds` and `TimeoutSeconds` are editable. Id, secret and the three URLs are hidden: a stolen admin session must not send logins to another host.
9. **install.sh** sends the secret in a file (mode 600) inside the copied setup folder, never on a command line, so it is not seen in the process list.

## New settings (starting values)

| Key | Start | Panel |
|---|---|---|
| `Identity:GitHub:ClientId` | empty (off) | no |
| `Identity:GitHub:ClientSecret` | empty | no |
| `Identity:GitHub:CallbackUrl` | `https://dalanet.ir/api/auth/github/callback` | no |
| `Identity:GitHub:WebUrl` | `https://github.com` | no |
| `Identity:GitHub:ApiUrl` | `https://api.github.com` | no |
| `Identity:GitHub:StateSeconds` | 600 | yes |
| `Identity:GitHub:TimeoutSeconds` | 10 | yes |
