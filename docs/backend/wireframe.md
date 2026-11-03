# Backend Wireframe

- Pages call one service method; services own the rules.
- Every flow checks the client owns the booking/invoice/ticket.

## Create booking

```mermaid
sequenceDiagram
  actor C as Client
  participant P as /bookings/new
  participant B as BookingService
  participant Pr as PricingService
  participant I as InvoiceService
  C->>P: submit (offer, travellers)
  P->>B: createBooking(...)
  B->>Pr: quote(offer, travellers)
  Pr-->>B: quote
  B->>B: save booking (pending)
  B->>I: createInvoices(booking)
  B-->>P: bookingId
  P-->>C: redirect
```

## Payment

```mermaid
sequenceDiagram
  actor C as Client
  participant E as /payments/new
  participant P as PaymentService
  participant I as InvoiceService
  participant B as BookingService
  C->>E: pay (invoiceId)
  E->>P: takePayment(clientId, invoiceId)
  P->>I: getInvoice(invoiceId)
  P->>P: validate (owner, invoice open), save payment (paid)
  P->>I: markPaid(invoiceId)
  P->>B: onInvoicePaid(bookingId)
  P-->>E: receipt
  E-->>C: confirmation
```

## Cancel booking

```mermaid
sequenceDiagram
  actor C as Client
  participant P as /bookings/{id}
  participant B as BookingService
  participant I as InvoiceService
  participant Pay as PaymentService
  C->>P: cancel (reason)
  P->>B: cancelBooking(...)
  B->>B: validate, calculate fee + refund
  B->>B: save cancellation, status cancelled
  B->>I: voidUnpaid(bookingId)
  B->>I: createCreditNote(bookingId, refund)
  B->>Pay: refund(bookingId, refund)
  B-->>P: summary
  P-->>C: redirect
```

Unpaid invoices are voided; paid amounts are refunded as a payment with status `refunded`.

## Ticket & SLA

```mermaid
sequenceDiagram
  actor C as Client
  participant P as /customerservice/tickets/new
  participant T as TicketService
  participant S as SlaService
  C->>P: submit (category, text, bookingId?)
  P->>T: createTicket(...)
  T->>S: resolveSla(category, bookingId)
  S-->>T: deadline
  T->>T: save ticket (open)
  T-->>P: ticketId
  P-->>C: confirmation
```

## Service methods

| Service          | Methods                                                                                                                                            |
| ---------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| `BookingService` | `createBooking(clientId, tripId, travelers)` → `bookingId`<br>`cancelBooking(clientId, bookingId, reason)` → summary<br>`onInvoicePaid(bookingId)` |
| `PricingService` | `quote(tripId, travelers)` → quote                                                                                                                 |
| `InvoiceService` | `createInvoices(booking)`<br>`getInvoice(invoiceId)`<br>`markPaid(invoiceId)`<br>`voidUnpaid(bookingId)`<br>`createCreditNote(bookingId, amount)`  |
| `PaymentService` | `takePayment(clientId, invoiceId)` → receipt<br>`refund(bookingId, amount)`                                                                        |
| `TicketService`  | `createTicket(clientId, category, text, bookingId?)` → `ticketId`                                                                                  |
| `SlaService`     | `resolveSla(category, bookingId?)` → deadline                                                                                                      |

`ClientService` and `TripService` aren't used in these flows.
