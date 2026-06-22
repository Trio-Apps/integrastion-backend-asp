# Dynamic Order Preparation Time Adjustment API

> Talabat POS API feature. A new endpoint that lets restaurant integrators **dynamically adjust the
> original preparation time** for ongoing restaurant orders, enabling real-time responsiveness to
> kitchen workload and operational changes.
>
> ⚠ **This API is limited to Platform Delivery orders.**

## Objective

This API aims to:

- **Enable dynamic prep-time modification** — give integrators the ability to offer restaurants the
  option to dynamically modify their order preparation time.
- **Real-time communication** — ensure changes in preparation time are communicated downstream to
  delivery riders and end customers in real time.
- **Improve operational efficiency** — better align order readiness with rider pickup time, reducing
  avoidable rider wait times and improving overall operational efficiency.

## How to Use: Adjusting Preparation Time

### New endpoint

```http
POST /v2/orders/{orderToken}/adjust-preparation-time
```

> Detailed docs: *POS Middleware API — Adjust Preparation Time.*

### The `PreparationTimeAdjustments` payload

In the dispatch order payload, vendors receive a new field called `PreparationTimeAdjustments`.
This field provides the permissible range for time adjustments for a specific order:

```json
"PreparationTimeAdjustments": {
  "maxPickUpTimestamp": "2016-03-14T17:00:00.000Z",
  "minPickupTimestamp": "2016-03-14T17:00:00.000Z",
  "preparationTimeChangeIntervalsInMinutes": []
}
```

| Field | Meaning |
|-------|---------|
| `maxPickUpTimestamp` | The latest possible pickup timestamp allowed for this order. |
| `minPickupTimestamp` | The earliest possible pickup timestamp allowed for this order. |
| `preparationTimeChangeIntervalsInMinutes` | The valid intervals (in minutes) by which prep time can be adjusted. If **empty**, the default global range applies. |

> For more details on the dispatch order payload, see: *POS Plugin API — Dispatch Order.*

## Controlled Value Range per Vendor

The valid range for updating the preparation time is controlled to ensure optimal order flow, rider
utilization, and customer experience.

- **Configurable per country** — initially, this range is configurable and enforced at the country level.
- **Default global range** — if a country has no specific configuration, the default acceptable
  range is **−10 to +10 minutes** (adjust prep time up to 10 minutes earlier or later than the
  original estimate), regardless of the market.
- **Order-specific range** — for each order, the valid adjustment range is explicitly shared within
  the `PreparationTimeAdjustments` field in the dispatch order payload.

## Valid States

The prep-time adjustment can happen **any time before order acceptance, and even post-acceptance
until the rider has been assigned.**

| Vendor accepted | Rider accepted | Adjustment allowed |
|:---:|:---:|:---:|
| No | No | ✅ Yes |
| No | Yes | ✅ Yes |
| Yes | Yes | ❌ No |
| Yes | No | ✅ Yes |
