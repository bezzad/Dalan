# Dalan — Business Notes

Nov 16, 2026 · Research for Behzad Khosravifar

This file answers three questions:

1. Is ngrok a real business?
2. Is there real demand for Dalan in Iran, or is it only a personal tool?
3. What should Dalan charge for, and how much?

Every fact is marked:

- **[S]** sourced. The link is next to it.
- **[E]** estimate or opinion. No direct source. Check before you rely on it.

Exchange rate used: **1 USD ≈ 269,000 Toman** (free market, 2026-11-14) [S] ([arzbin](https://www.arzbin.com/brief/iran-market-brief-2026-11-14-morning)).

## 1. ngrok as a business

### Short answer

Yes. ngrok is well known among developers worldwide and makes real money. But it is a mid-size developer tool, not a giant company.

### Facts

| Fact | Value | Mark |
| --- | --- | --- |
| Funding | $50M Series A, Dec 2022, led by Lightspeed, with Coatue. Its first outside money. | [S] [ngrok blog](https://ngrok.com/blog/series-a), [SiliconANGLE](https://siliconangle.com/2022/12/13/reverse-proxy-startup-ngrok-raises-50m-funding/) |
| Before funding | Funded only by customer revenue for years (bootstrapped). | [S] [ngrok blog](https://ngrok.com/blog/series-a) |
| Users | About 5 million developers. | [S] [ngrok blog](https://ngrok.com/blog/series-a), [VentureBeat](https://venturebeat.com/programming-development/how-ngrok-helps-speed-up-application-development-with-simplified-ingress-self-service-infrastructure) |
| Paying customers | More than 30,000 (in 2022). | [S] [VentureBeat](https://venturebeat.com/programming-development/how-ngrok-helps-speed-up-application-development-with-simplified-ingress-self-service-infrastructure) |
| Known customers | Zendesk (about 200 engineers), Copado, Veritas. | [S] same VentureBeat article |
| Revenue | Not published. Owler estimates $5M–$25M per year. | [E] third-party estimate, [Owler](https://www.owler.com/company/ngrok) |
| Employees | Owler estimates 25–100. | [E] [Owler](https://www.owler.com/company/ngrok) |
| Website traffic | About 1.5M visits per month. Top countries: India 16%, US 12%, Indonesia 7%. Iran is not in the list. | [S] [Similarweb](https://www.similarweb.com/website/ngrok.com/) |

**My check of the revenue [E]:** 30,000 paying customers × $15–$40 per month ≈ $5M–$15M per year. This fits the Owler range. Only about 0.6% of users pay (30,000 of 5 million).

**Lesson for Dalan [E]:** a tunnel product gets many free users and few paying ones. ngrok makes most of its money from companies (teams, security, custom domains), not from hobby developers.

### ngrok prices today [S]

From [ngrok pricing](https://ngrok.com/pricing.md):

| Plan | Price | In Toman (≈) | Main limits |
| --- | --- | --- | --- |
| Free | $0 | 0 | 3 endpoints, 1 GB out, 20k requests, inspector 24 h, no custom domain |
| Hobbyist | $8–$10 / month | 2.2M–2.7M | 3 endpoints, 5 GB, 100k requests, inspector 72 h, 10 ngrok domains |
| Pay-as-you-go | $20 base / month | 5.4M + usage | unlimited endpoints, $0.10/GB, $1 per 100k requests, custom domain $0.01/hour |
| Enterprise | from $10,000 / year | 2.7B / year | custom |

So ngrok charges for: **online endpoints, data out (GB), request count, custom domains, inspector days, and enterprise security.**

## 2. The Iran market

### Short answer [E]

There is real demand, but it is small and price-sensitive at first. The pure tunnel alone will not make much money in Iran, because free options exist. Dalan can earn money if it sells the **combination**: works on the domestic network + fixed address + phone-verified users + bots + protection for company tools.

Honest view: in year one Dalan is mostly a personal tool that can **pay for its own server**. It becomes a real side income only if companies and app builders pay for identity and private access.

### Signs of demand

| Signal | What it shows | Mark |
| --- | --- | --- |
| Bale has about 64.5M users; it ran a 100M-Toman bot building contest; its bot page says "a community of 60 million". | Iranian users already use bots a lot, and platforms push builders to make bots. | [S] [Zoomit report](https://www.zoomit.ir/business/469774-bale-messenger-performance-report-ai/), [Zoomit contest](https://www.zoomit.ir/tech-iran/435421-bale-competition/), [bale.ai/dev](https://bale.ai/dev) (titles seen in search; pages blocked for our reader) |
| Bale made about 250 billion Toman from its AI service. | Iranians do pay for digital services inside Iran. | [S] [Digiato](https://digiato.com/iran-technology-news/bale-generates-250-billion-toman-revenue-ai-service) (headline only) |
| Rubika and Bale grew fast in 1404, per the Information Technology Organization. | Domestic platforms are growing because foreign ones are filtered. | [S] [Peivast](https://peivast.com/p/256584) (headline only) |
| During the 2026 shutdown, AWS, Vercel and foreign CI stopped for Iranian developers; illegal "Internet Pro" access cost 10–100× more per GB. | Builders need something that works inside Iran when international links are cut. | [S] [dev.to article](https://dev.to/dalirnet/from-80-days-around-the-world-to-80-days-in-the-dark-1bkb) |
| Persian Q&A threads ask how to open a local server or office server to the internet. | Real people have the "no public IP" problem. | [S] [Roocket](https://roocket.ir/discuss/%D8%B9%D9%85%D9%88%D9%85%DB%8C-%DA%A9%D8%B1%D8%AF%D9%86-%D8%AF%D8%B3%D8%AA%D8%B1%D8%B3%DB%8C-%D8%A8%D9%87-IP/), [Tosinso](https://tosinso.com/questions/211942/%D8%AF%D8%B3%D8%AA%D8%B1%D8%B3%DB%8C-%D8%A8%D9%87-%D8%B3%D8%B1%D9%88%D8%B1-%D9%84%D9%88%DA%A9%D8%A7%D9%84%D8%8C-%D8%A8%D8%A7-%D8%A7%DB%8C%D9%86%D8%AA%D8%B1%D9%86%D8%AA-%D8%AF%D8%B1-%D8%AE%D8%A7%D8%B1%D8%AC-%D8%A7%D8%B2-%D8%AF%D9%81%D8%AA%D8%B1) |
| ngrok gets no visible Iranian traffic. | Foreign tunnels are not used much (sanctions, filtering). This is space for a local one. | [S] Similarweb above; reason is [E] |

### Iranian competitors and alternatives

| Name | What it is | Price | Mark |
| --- | --- | --- | --- |
| **DNSLab** (dnslab.link) | Iranian dynamic DNS **and reverse proxy without a static IP** (TCP, UDP, HTTP), Persian docs. The closest local competitor. | Presented as free; it has a plans page we could not open. | [S] [GitHub README](https://github.com/AkbarAsghari/DNSLab.Web), [DNSLab blog: "ngrok alternative"](https://dnslab.link/blog/ngrok-alternative-dnslab); price is **unchecked** |
| frp, sish (self-hosted) | Free tunnel you run on your own Iran VPS. | Free + a VPS | [E] |
| ParsPack VPS (Iran) | Rent a server instead of using the home machine. | From about 955,000 Toman / month (1 vCPU, 1 GB) | [S] [ParsPack](https://parspack.com/vps/cheap) |
| ParsPack PaaS (Iran) | Run code on their platform. | Per resource: 1 vCore 288,000, 1 GB RAM 360,000, 1 GB disk 18,000 Toman / month. A tiny app (0.5 core, 0.5 GB) ≈ 330,000 Toman / month. | [S] [ParsPack PaaS](https://parspack.com/paas); tiny-app sum is [E] |
| Liara | Iranian PaaS; has a free cloud host product. | Free tier + paid | [S] [Liara free host](https://liara.ir/products/free-cloud-host/) (title only) |
| Bale, Eitaa, Rubika bots | Bots inside state-linked messengers. Builder must run a public server. | Free platform | [E] |

Note: DNSLab means section 15 of `DESIGN.md` ("found none") was wrong. A local tunnel exists. Dalan must not compete on the plain tunnel; it must compete on the combination.

### Who will pay [E]

Ordered from most likely to least likely to pay:

1. **Small companies** that want to open internal tools (Jira, GitLab, a dashboard) to staff or clients, safely. They need a fixed address, custom domain, Basic Auth and IP allowlist. Companies pay; students do not.
2. **App and bot builders who need real users** and do not want to build SMS login. "Login with Dalan" saves them an SMS panel, OTP templates and abuse work.
3. **Builders who need a bot that works during a shutdown** (moving from Telegram).
4. **Hobby developers and students.** Many users, little money. Keep them on the free plan; they bring growth and showcase apps.

## 3. Pricing proposal for Dalan

All of this is **[E]**, a starting proposal for discussion.

### Rules

- **Free until there are real users.** `DESIGN.md` already says: start charging when about ten customers want to pay.
- **Much cheaper than renting a server.** The builder brings the compute (home machine). Dalan only carries traffic. So every paid plan should cost less than the cheapest Iran VPS (about 955,000 Toman).
- **Charge for what costs us money or brings business value.** Do not charge for what makes growth hard.
- **Prices in Toman, reviewed every 3 months** because of inflation. Offer monthly and 3-month periods, not yearly.

### What to charge for

| Item | Charge? | Why |
| --- | --- | --- |
| Data traffic (GB) | **Yes**, main limit | It is our real cost on an Iranian data centre. |
| Number of online bots / sites | **Yes** | Simple to understand. Same idea as ngrok endpoints. |
| Custom domain | **Yes** | Extra work (certificate, DNS) and high value for companies. |
| Inspector history (days) | **Yes** | Uses disk; useful mostly to serious builders. |
| Basic Auth, IP allowlist | **Yes**, paid plans only | Companies need it; they can pay. |
| Team members | **Yes**, business plan | Companies share one account. |
| "Login with Dalan" for outside sites | **Yes**, after a free quota | Each new phone check costs an SMS. |
| Fixed subdomain (`name.dalanet.ir`) | **No**, free for everyone | It is our main advantage over Cloudflare Tunnel and ngrok free. |
| Number of messages | **No** | Hard to understand. Use rate limits for abuse instead. |
| Number of a bot's users | **No** | Punishes success and slows growth. |
| Verified users of Dalan's own app | **No** | Users verify once for the whole platform. |

### Suggested plans

| | Free | Personal | Pro | Business |
| --- | --- | --- | --- | --- |
| Price / month | 0 | **190,000 T** (≈ $0.7) | **490,000 T** (≈ $1.8) | **from 1,500,000 T** (≈ $5.6) |
| Online bots + sites | 1 + 1 | 3 + 3 | 10 + 10 | 25 + 25 |
| Fixed subdomain | yes | yes | yes | yes |
| Traffic / month | 1 GB | 10 GB | 50 GB | 200 GB |
| Custom domain | no | no | 1 | 5 |
| Inspector history | 24 hours | 3 days | 7 days + replay | 30 days + replay |
| Basic Auth / IP allowlist | no | Basic Auth | both | both |
| Team members | 1 | 1 | 1 | 5 |
| "Login with Dalan" (new phone checks / month) | 50 | 200 | 1,000 | 5,000 |
| Showcase | yes | yes | featured | featured |

**Add-ons:**

- Extra 10 GB traffic: about 50,000 T.
- Extra custom domain: about 100,000 T / month.
- Extra 1,000 phone checks: price = real SMS cost × 2 (check the SMS provider's price first).

**Compared with others:**

- ngrok Hobbyist ≈ 2.2M–2.7M T. Dalan Personal is about 12× cheaper.
- Smallest ParsPack VPS ≈ 955,000 T. Dalan Pro is about half.
- DNSLab is free (as far as we know). So the free plan must stay useful, and paid plans must sell what DNSLab does not give: identity, bots, inspector, security, custom domain.

### Our costs to check before launch

| Cost | Value | Mark |
| --- | --- | --- |
| Server, 2 vCPU / 4 GB / 60 GB, Iran | ≈ 2.5M–3.5M T / month (ParsPack PaaS rates: 576k CPU + 1.44M RAM + 1.08M disk) | [E] from [S] unit prices |
| Traffic price at the data centre | Not checked | open |
| One SMS (OTP) | Not checked; Kavenegar and SMS.ir pages were blocked for our reader | open |
| Payment gateway (Zarinpal, IDPay, …) | Needs a business registration and eNamad | [E] |

### Revenue scenarios [E]

| Stage | Paying customers | Revenue / month | Covers |
| --- | --- | --- | --- |
| Year 1 | 30 (mostly Personal) | ≈ 7M–10M T | Server + SMS |
| Year 2 | 200 Personal/Pro + 10 Business | ≈ 80M T (≈ $300) | Costs + small side income |
| Good case | 1,000 + 50 Business | ≈ 400M T (≈ $1,500) | A real small business |

ngrok's own ratio (about 0.6% of users pay) suggests that 200 paying customers need about 30,000 builders. The Iranian ratio may be lower. So growth must come from bots and the showcase, where end users (not builders) bring builders.

## 4. Recommendation [E]

1. Keep the plan in `DESIGN.md`: personal tool first, then known apps, then public. Do not build billing in version 1.
2. Build the free plan limits (traffic, bots, sites) as admin settings from the start, so paid plans later are only new numbers.
3. Sell to **companies** first when charging starts: custom domain, IP allowlist, team. They pay more and complain less about price.
4. Treat **identity ("Login with Dalan")** as the second product to sell. It is the clearest pain in Iran.
5. Before charging, check: data centre traffic price, SMS price, DNSLab plans, and the legal side of selling a relay service.

## 5. Open questions

- [ ] What do DNSLab paid plans cost and include?
- [ ] What does one OTP SMS cost at Kavenegar and SMS.ir today?
- [ ] What does traffic cost per GB at the chosen data centre?
- [ ] Which payment gateway, and what registration does it need?
- [ ] Do the plan prices above feel right to the owner and the first known users (a friend's game, a personal site)?
