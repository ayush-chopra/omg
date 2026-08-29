# API decisions and open approvals

## Confirmed contracts

### OMG Company Stores

The Company Store is hosted on a `mybrightsites.com` hostname. API v2.7.0 accepts the application token in `X-Application-Token`.

Order polling uses `updated_at_from`, `updated_at_to`, `page`, and `per_page`. A complete order contains shipping contact/address, totals, and line items. Shipment creation requires a top-level `shipment` object and numeric line-item IDs and quantities.

Official documentation: <https://docs.mybrightsites.com/>

### Cloud9 v9.2

Cloud9 authentication is `POST /Auth/Authenticate`. Staging uses `POST /Data/AddShipJob`; Cloud9 updates an existing job when the same order number and location ID are supplied. A job requires either a shop code or a carrier SCAC plus service code.

Cloud9 sends `orderNumber`, `shipDate`, `cost`, `isReturn`, `voidDate`, and `pkgs[].trackingNumber` to a customer-owned callback endpoint.

Official documentation: <https://docs.cloud9express.com/help/v9.2/restapi.html>

## Data ownership

| Value | System of record | Connector action |
|---|---|---|
| Order and line items | OMG | Read and stage in Cloud9 |
| Final rate/label | Cloud9 | Produced by Cloud9 fulfillment workflow |
| Tracking number | Cloud9 | Write to an OMG shipment |
| Actual shipping cost | Cloud9 | Store internally in the embedded database |
| Package dimensions/weight | Fulfillment/Cloud9 | Seed defaults, then verify before shipping |

## Why cost is not posted to OMG

The documented OMG create-shipment payload accepts shipping method, tracking number, ship date, note, shipping-confirmation flag, and line items. It does not document a cost input. Although an order response can show shipment cost, that does not make the field writable.

The connector therefore records Cloud9 cost in integer cents internally. It does not put cost in the free-text note because that would not be a structured, reportable cost field and was not approved by the client.

## Manual-review cases

- Multiple packages: the callback does not say which OMG line-item quantity belongs to each tracking number.
- Returns: outbound OMG shipment creation is not a return-management API.
- Voids: the connector does not automatically delete an OMG shipment without an approved reconciliation policy.
- International orders: Cloud9 requires customs data not yet mapped from OMG.

## Approvals still required

1. OMG must confirm the target client account is a Company Store and enable API access.
2. OMG must confirm production rate limits and whether it recommends shipment creation or order update for this client.
3. Cloud9 must confirm the production base URL, shop-code behavior, and callback authentication support.
4. The client must approve default carton data and manual-review ownership.
5. Both vendors must support end-to-end UAT. No production guarantee should be given before this test.
