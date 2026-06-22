# Talabat POS Guide — Item Prices & Discounts Mapping

> *Integrator Copy.* This guide outlines recent updates to the **order payload structure**,
> specifically around **original product prices** and **product-level discounts**.

## Context

These changes aim to:

- Provide greater transparency in pricing.
- Enable accurate reporting.
- Ensure consistent discount handling across all systems.

> Review carefully to ensure your integration aligns with the latest specifications.

## Changes to the Order Payload

### Product-level prices

Talabat now sends **original (undiscounted)** prices for products and toppings instead of the
previously sent discounted prices.

**New/updated fields under `products[]`:**

| Field | Type | Description |
|-------|------|-------------|
| `paidPrice` | string | Total original price (excluding discounts) of the product (gross, including tax), based on quantity. **Excludes toppings.** Typically `unitPrice × quantity`. |
| `unitPrice` | string | Base original price for one unit of the item (excluding topping & discounts). |
| `selectedToppings[].price` | string | Original price of each topping, based on quantity. |

**Example:**

```json
"product": {
  "paidPrice": "28.00",
  "unitPrice": "14.00",
  "selectedToppings": [
    { "name": "extra cheese", "price": "1.50" }
  ]
}
```

### Product-level discounts and sponsorship

Talabat now provides detailed discount information at the **product and topping level**, including
sponsorship breakdowns.

**Sponsors** — each discount can be co-funded by up to three sponsors:

- `PLATFORM` — the delivery platform
- `VENDOR` — the vendor (restaurant)
- `THIRD_PARTY` — a third-party contributor

> - If a sponsor is not listed or their amount is `0`, the discount is not attributed to them.
> - If the `sponsorships` array is missing or empty, no sponsorship breakdown is provided.

**New/updated fields:**

| Field | Type | Description |
|-------|------|-------------|
| `discounts` | array | An array of discounts applied to the product or topping. |
| `discounts[].name` | string | Name of the discount (e.g., `"First Order"`). |
| `discounts[].amount` | string | Total discount amount applied (for total quantity). |
| `discounts[].sponsorships` | array | List of sponsors contributing to the discount. |
| `discounts[].sponsorships[].sponsor` | string | Sponsor name (enum: `PLATFORM`, `VENDOR`, `THIRD_PARTY`). |
| `discounts[].sponsorships[].amount` | string | Amount contributed by the sponsor (for total quantity). |

**Example:**

```json
"product": {
  "discounts": [
    {
      "name": "First Order",
      "amount": "6.00",
      "sponsorships": [
        { "sponsor": "PLATFORM", "amount": "3.00" },
        { "sponsor": "VENDOR", "amount": "3.00" }
      ]
    }
  ],
  "selectedToppings": [
    {
      "name": "extra cheese",
      "price": "1.50",
      "discounts": [
        {
          "name": "First Order",
          "amount": "1.50",
          "sponsorships": [
            { "sponsor": "PLATFORM", "amount": "0.75" },
            { "sponsor": "VENDOR", "amount": "0.75" }
          ]
        }
      ]
    }
  ]
}
```

## Updated Full Order Payload Example

A full example of an order payload, with the highlighted modifications for:

- **Product-level:** `paidPrice`, `unitPrice`, and `discounts`
- **Order-level:** `discounts`

> ⚠ **Note:** There is no structural change to the payload. Only previously missing or incorrect
> values are now populated with accurate data.

```json
{
  "token": "5f373562-591a-4db9-8609-7eec7880f28d",
  "code": "n0s1-w0k1",
  "comments": {
    "customerComment": "Please hurry, I am hungry"
  },
  "createdAt": "2016-03-14T17:00:00.000Z",
  "customer": {
    "email": "s188sduisddsnjknsj",
    "firstName": "food",
    "lastName": "panda",
    "mobilePhone": "+49 99999999",
    "flags": ["string"]
  },
  "delivery": {
    "address": {
      "postcode": "10117",
      "city": "Berlin",
      "street": "Oranienburger Staße",
      "number": "67"
    },
    "expectedDeliveryTime": "2016-03-14T17:50:00.000Z",
    "expressDelivery": false,
    "riderPickupTime": "2016-03-14T17:35:00.000Z"
  },
  "expeditionType": "pickup",
  "expiryDate": "2016-03-14T17:15:00.000Z",
  "extraParameters": {
    "property1": "string",
    "property2": "string"
  },
  "invoicingInformation": {
    "carrierType": "string",
    "carrierValue": "string"
  },
  "localInfo": {
    "countryCode": "DE",
    "currencySymbol": "€",
    "platform": "Foodpanda",
    "platformKey": "FP_DE"
  },
  "payment": {
    "status": "paid",
    "type": "paid"
  },
  "test": false,
  "shortCode": "42",
  "preOrder": false,
  "pickup": null,
  "platformRestaurant": {
    "id": "sq-abcd"
  },
  "price": {
    "deliveryFees": [
      { "name": "packaging fee", "value": 2.5 }
    ],
    "grandTotal": "25.50",
    "payRestaurant": "25.50",
    "riderTip": "1.20",
    "totalNet": "19.45",
    "vatTotal": "2.50",
    "collectFromCustomer": "16.34"
  },
  "products": [
    {
      "categoryName": "Burgers",
      "name": "Double Cheese Burger",
      "paidPrice": "12.00",
      "unitPrice": "6",
      "quantity": "2",
      "remoteCode": "ID_FOR_DOUBLE_CHEESE_BURGER_ON_POS",
      "comment": "No cheese please",
      "id": "ID_FOR_DOUBLE_CHEESE_BURGER_ON_PLATFORM",
      "itemUnavailabilityHandling": "CALL_CUSTOMER_AND_REPLACE",
      "variation": {
        "name": "Double Cheese Burger"
      },
      "discounts": [
        {
          "name": "First order",
          "amount": "1.50",
          "sponsorships": [
            { "sponsor": "PLATFORM", "amount": "0.50" },
            { "sponsor": "VENDOR", "amount": "0.50" },
            { "sponsor": "THIRD_PARTY", "amount": "0.50" }
          ]
        }
      ],
      "selectedToppings": [
        {
          "children": [],
          "name": "extra cheese",
          "price": "1.50",
          "quantity": 1,
          "id": "ID_FOR_EXTRA_CHEESE_ON_PLATFORM",
          "remoteCode": "ID_FOR_EXTRA_CHEESE_ON_POS",
          "type": "PRODUCT",
          "itemUnavailabilityHandling": "REMOVE",
          "discounts": [
            {
              "name": "First order",
              "amount": "1.50",
              "sponsorships": [
                { "sponsor": "PLATFORM", "amount": "0.50" },
                { "sponsor": "VENDOR", "amount": "0.50" },
                { "sponsor": "THIRD_PARTY", "amount": "0.50" }
              ]
            }
          ]
        }
      ]
    }
  ],
  "corporateTaxId": "example-tax-id",
  "callbackUrls": {
    "orderAcceptedUrl": "string",
    "orderRejectedUrl": "string",
    "orderPickedUpUrl": "string",
    "orderPreparedUrl": "string",
    "orderProductModificationUrl": "string",
    "orderPreparationTimeAdjustmentUrl": "string"
  }
}
```

## List of Test Scenarios

| Test case | Description |
|-----------|-------------|
| Normal item without discounts | Items without any applied discounts display correctly with original pricing and no discount info. |
| Base discount only (30% selected items, no booster) | Items with a base discount and 50/50 sponsorship split, without any booster applied. |
| Base discount + booster (30% base + booster) | Items with both base discount and booster applied, including different sponsorship splits per discount component. |
| Variation item with price on selection (discounted choices) | Variation items where the base item has zero price but discounts are applied to the selectable choices/sub-items. |
| Item with discounts on item AND choices | Correct handling when the base discount is applied to both the main item and its choices/sub-items. |
| Mixed order (no discount + base only + base + booster) | Orders containing items with varying discount configurations in a single order. |

## Summary

- Talabat now provides **original item prices** (`unitPrice`, `paidPrice`, and `selectedToppings[].price`).
- Discounts now include **item-level details and sponsorship splits**.
- **No structural changes** to the payload; the data has been improved for accuracy and transparency.
