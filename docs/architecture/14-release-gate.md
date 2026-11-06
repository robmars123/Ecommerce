## 14. Production readiness (release gate)

- [ ] Tests 1–19 (§11.2) pass on real Postgres; frontend tests F1–F8 (§11.3) pass.
- [ ] Stripe test mode end to end: checkout → webhook → Paid → email → shipped → delivered.
- [ ] Reservation expiry proven in staging with a short TTL.
- [ ] Dead-letter and projector-lag alerts wired (log + admin view at minimum).
- [ ] Secret scan clean on the release branch (`sk_live`, `sk_test`, `whsec_`, private keys).
- [ ] Migrations backward-compatible and applied to a staging DB that mirrors prod.
- [ ] Rate limits on login, checkout, and webhook; Stripe retries still accepted.
- [ ] Audit log covers stock adjusts, refunds, and overrides.
- [ ] Rollback tested: previous image runs against the current schema.
- [ ] README covers local run (Postgres via Docker, `stripe listen`) and tests.


