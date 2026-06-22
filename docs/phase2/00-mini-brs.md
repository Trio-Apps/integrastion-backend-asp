# Mini BRS — Talabat × Foodics Integration (PICK) — Phase 2

> Business Requirements Specification for Phase 2 enhancements of the Talabat × Foodics
> integration for PICK operations. The purpose is to improve operational control, item
> availability management, order monitoring, and user authorization handling through the
> integration console.

## 1. User Management & Authorization

- The admin user shall be able to create multiple users within the console.
- The admin user shall be able to create and manage roles with specific authorizations.
- Users can be assigned to one or multiple authorization roles.
- Authorization rules shall define accessible branches and permitted actions.
- Branch visibility and actions must be restricted based on assigned permissions.

## 2. Orders Console

- The system shall display all Talabat orders within the console.
- The order listing shall include: **Order ID, Customer ID, Customer Name, Customer Address,
  Channel** (currently Talabat), **TMP/TGO indicator,** and **Payment Method.**
- Each order shall support expandable dropdown details.
- Expanded order details shall include: **Date, Items, Modifiers, Quantity, Amount,
  Total Amount,** and **Discount.**
- The console shall support real-time or near real-time synchronization of orders.

## 3. Item & Modifier Availability Management

- Item and modifier active/inactive status shall be synchronized from Foodics.
- Inactive items/modifiers from Foodics shall **not** be displayed in the console nor the Talabat app.
- Authorized users shall be able to search using item code, item name, modifier code, or modifier name.
- Users shall have a toggle option to mark items/modifiers as **In Stock** or **Out of Stock**.
- Upon changing availability, a popup shall allow selection of one or multiple branches.
- Availability changes shall reflect in Talabat.
- If an item becomes Out of Stock in Talabat, the status shall also reflect in the console.
- Out of Stock functionality shall support two options: **'For a Day'** and **'Till Further Notice'**.
  - If **'For a Day'** is selected, the item shall automatically return to In Stock status at the
    next day's branch opening hour.
  - The console shall display branch opening hours for reference.
  - If **'Till Further Notice'** is selected, the item shall remain Out of Stock until manually
    updated by an authorized user.

## 4. Smart Search

- The console shall support smart search functionality.
- Users shall be able to search using any relevant keyword or partial data.
- Search should support order details, item information, customer details, modifier data, and
  branch-related information.

## 5. Advanced Order Filtering

- The Orders page shall support advanced filtering options.
- Filters shall include: **Date, Branch, Status, Customer Name,** and **Customer Phone Number.**
- Multiple filters shall be usable simultaneously.
