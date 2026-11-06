## 1. Context

**Builder:** Nathaniel — Sr. Software Developer (.NET/C#, React/TypeScript), Windows 11, no Mac, solo. Goal: staff engineer. This build is practice and an interview anchor: a familiar domain, so the conversation goes straight to the hard parts.

**Depth, not breadth.** The value is inventory races, idempotent payments, reservation expiry, outbox delivery, and failure handling — easy to fake in a demo, hard to get right in production.

**Done means:** a customer can browse, search, add to cart, check out, pay, get a confirmation, track fulfillment, and request a return — with no oversell, no double charge, and no lost order on crash or webhook retry. An admin can manage products, inventory, orders, and customers. Every hard invariant has a test written first.

**Not in scope:** marketplace, ML recommendations, microservices, store submission, go-to-market. Full list: app context §6.


