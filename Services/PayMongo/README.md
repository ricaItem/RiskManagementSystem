# PayMongo integration

Server-side service and API for PayMongo payment intents (card with 3DS, GCash, Maya).

## Config

- **appsettings.json** → `PayMongo:SecretKey`, `PayMongo:PublicKey`, `PayMongo:BaseUrl`
- Test keys are set for PayMongo test mode.

## API (ready for front-end)

- **POST /api/paymongo/payment-intents**  
  Body: `{ "amountCentavos": 10000, "paymentMethodAllowed": ["card", "paymaya", "gcash"] }`  
  Returns: `{ "id", "clientKey", "amount", "currency", "status", "nextAction" }`  
  Use `clientKey` and `id` on the client to attach payment method (PayMongo.js or redirect).

- **GET /api/paymongo/payment-intents/{id}**  
  Returns current payment intent status (e.g. after 3DS or e-wallet return). Poll until `status` is `succeeded` or `awaiting_payment_method`.

## PayMongo test cards (3DS)

- **4120 0000 0000 0007** – 3DS required; complete auth to succeed.
- **4230 0000 0000 0004** – 3DS required; declined before auth.
- **5234 0000 0000 0106** – 3DS required; declined after auth.
- **5123 0000 0000 0001** – 3DS not required; can succeed without 3DS.

Use any future expiry and any 3-digit CVC. Amount minimum in test is 10000 centavos (₱100.00).

## Integration steps (when wiring UI)

1. On billing step, call `POST /api/paymongo/payment-intents` with amount (e.g. plan total in centavos) and `paymentMethodAllowed` based on selected method (card / gcash / paymaya).
2. Use returned `clientKey` with PayMongo.js to attach payment method (card tokenization or e-wallet redirect).
3. If `nextAction.redirectUrl` is present, redirect user (3DS or e-wallet); after return, poll `GET /api/paymongo/payment-intents/{id}` until `status === "succeeded"`.
4. Optionally handle webhook `payment.paid` for e-wallets (separate endpoint to add).
