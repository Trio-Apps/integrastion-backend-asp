# Foodics-Talabat Integration Test Data Plan

## Collection Analysis Summary

**Collection:** Foodics Sandbox
**Collection ID:** 12882056-288945c6-2cf6-4f05-bc30-8cef666bdf74
**Base URL:** `https://api-sandbox.foodics.com/v5`
**Authentication:** Bearer Token (Pre-configured)

### Key Endpoints Analyzed

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/products` | GET | List all products |
| `/products` | POST | Create new product |
| `/groups` | GET | List all groups |
| `/groups` | POST | Create new group |
| `/categories` | GET | List all categories |

---

## Test Products for Integration Testing

### Product 1: Classic Burger 🍔

**Purpose:** Test basic product creation and single group mapping

```json
{
    "name": "Classic Burger",
    "name_localized": "برغر كلاسيك",
    "description": "Juicy beef patty with lettuce, tomato, and special sauce",
    "description_localized": "قطعة لحم بقري مع الخس والطماطم والصلصة الخاصة",
    "image": "https://example.com/images/classic-burger.jpg",
    "is_active": true,
    "is_stock_product": false,
    "pricing_method": 1,
    "selling_method": 2,
    "costing_method": 1,
    "price": 3500,
    "cost": 1800,
    "calories": 650,
    "preparation_time": 15,
    "sku": "TEST-BURGER-001",
    "barcode": "1234567890001",
    "category_id": "{{CATEGORY_ID}}",
    "tax_group_id": "{{TAX_GROUP_ID}}",
    "tags": [],
    "groups": [
        {
            "id": "{{MAIN_DISHES_GROUP_ID}}"
        }
    ]
}
```

**Mapping Details:**
- **Group:** Main Dishes
- **Price:** 35.00 SAR
- **Cost:** 18.00 SAR (51% margin)
- **Prep Time:** 15 minutes
- **Integration Test:** Basic product sync to Talabat

---

### Product 2: Chicken Caesar Salad 🥗

**Purpose:** Test product with multiple group associations

```json
{
    "name": "Chicken Caesar Salad",
    "name_localized": "سلطة سيزر بالدجاج",
    "description": "Fresh romaine lettuce with grilled chicken, croutons, and Caesar dressing",
    "description_localized": "خس طازج مع دجاج مشوي، خبز محمص، وصلصة السيزر",
    "image": "https://example.com/images/chicken-caesar-salad.jpg",
    "is_active": true,
    "is_stock_product": false,
    "pricing_method": 1,
    "selling_method": 2,
    "costing_method": 1,
    "price": 2800,
    "cost": 1200,
    "calories": 420,
    "preparation_time": 10,
    "sku": "TEST-SALAD-001",
    "barcode": "1234567890002",
    "category_id": "{{CATEGORY_ID}}",
    "tax_group_id": "{{TAX_GROUP_ID}}",
    "tags": [
        {
            "id": "{{HEALTHY_TAG_ID}}"
        }
    ],
    "groups": [
        {
            "id": "{{SALADS_GROUP_ID}}"
        },
        {
            "id": "{{HEALTHY_OPTIONS_GROUP_ID}}"
        }
    ]
}
```

**Mapping Details:**
- **Groups:** Salads, Healthy Options (Multiple groups)
- **Price:** 28.00 SAR
- **Cost:** 12.00 SAR (57% margin)
- **Prep Time:** 10 minutes
- **Integration Test:** Multi-group product mapping to Talabat categories

---

### Product 3: Margherita Pizza 🍕

**Purpose:** Test product with modifiers and customization options

```json
{
    "name": "Margherita Pizza",
    "name_localized": "بيتزا مارغريتا",
    "description": "Classic Italian pizza with tomato sauce, mozzarella, and fresh basil",
    "description_localized": "بيتزا إيطالية كلاسيكية مع صلصة الطماطم، موتزاريلا، والريحان الطازج",
    "image": "https://example.com/images/margherita-pizza.jpg",
    "is_active": true,
    "is_stock_product": false,
    "pricing_method": 1,
    "selling_method": 2,
    "costing_method": 1,
    "price": 4200,
    "cost": 2000,
    "calories": 850,
    "preparation_time": 20,
    "sku": "TEST-PIZZA-001",
    "barcode": "1234567890003",
    "category_id": "{{CATEGORY_ID}}",
    "tax_group_id": "{{TAX_GROUP_ID}}",
    "tags": [
        {
            "id": "{{POPULAR_TAG_ID}}"
        }
    ],
    "groups": [
        {
            "id": "{{PIZZAS_GROUP_ID}}"
        }
    ]
}
```

**Mapping Details:**
- **Group:** Pizzas
- **Price:** 42.00 SAR
- **Cost:** 20.00 SAR (52% margin)
- **Prep Time:** 20 minutes
- **Integration Test:** Product with size/topping modifiers for Talabat

---

## Test Groups (Menu Categories)

### Group 1: Main Dishes

```json
{
    "name": "Main Dishes",
    "name_localized": "الأطباق الرئيسية",
    "image": "https://example.com/images/main-dishes.jpg",
    "items_index": 1,
    "is_active": true,
    "products": [
        {
            "id": "{{CLASSIC_BURGER_ID}}"
        }
    ]
}
```

### Group 2: Salads

```json
{
    "name": "Salads",
    "name_localized": "السلطات",
    "image": "https://example.com/images/salads.jpg",
    "items_index": 2,
    "is_active": true,
    "products": [
        {
            "id": "{{CHICKEN_CAESAR_SALAD_ID}}"
        }
    ]
}
```

### Group 3: Pizzas

```json
{
    "name": "Pizzas",
    "name_localized": "البيتزا",
    "image": "https://example.com/images/pizzas.jpg",
    "items_index": 3,
    "is_active": true,
    "products": [
        {
            "id": "{{MARGHERITA_PIZZA_ID}}"
        }
    ]
}
```

### Group 4: Healthy Options

```json
{
    "name": "Healthy Options",
    "name_localized": "خيارات صحية",
    "image": "https://example.com/images/healthy-options.jpg",
    "items_index": 4,
    "is_active": true,
    "products": [
        {
            "id": "{{CHICKEN_CAESAR_SALAD_ID}}"
        }
    ]
}
```

---

## Test Data Summary Table

| Product Name | SKU | Price (SAR) | Cost (SAR) | Margin | Groups | Prep Time | Test Scenario |
|--------------|-----|-------------|------------|--------|--------|-----------|---------------|
| Classic Burger | TEST-BURGER-001 | 35.00 | 18.00 | 51% | Main Dishes | 15 min | Basic sync |
| Chicken Caesar Salad | TEST-SALAD-001 | 28.00 | 12.00 | 57% | Salads, Healthy Options | 10 min | Multi-group mapping |
| Margherita Pizza | TEST-PIZZA-001 | 42.00 | 20.00 | 52% | Pizzas | 20 min | Modifiers & options |

**Total Test Products:** 3
**Total Test Groups:** 4
**Average Price:** 35.00 SAR
**Average Margin:** 53%

---

## Product-Group Mapping Matrix

| Product | Main Dishes | Salads | Pizzas | Healthy Options |
|---------|-------------|--------|--------|-----------------|
| Classic Burger | ✅ | ❌ | ❌ | ❌ |
| Chicken Caesar Salad | ❌ | ✅ | ❌ | ✅ |
| Margherita Pizza | ❌ | ❌ | ✅ | ❌ |

---

## Integration Test Scenarios

### Scenario 1: Basic Product Sync
**Product:** Classic Burger
**Test:** 
1. Create product in Foodics
2. Verify sync to Talabat
3. Check product details match (name, price, description)
4. Verify single group assignment

### Scenario 2: Multi-Group Product
**Product:** Chicken Caesar Salad
**Test:**
1. Create product with multiple group associations
2. Verify product appears in both Talabat categories
3. Test filtering by group
4. Verify bilingual content (English/Arabic)

### Scenario 3: Product with Modifiers
**Product:** Margherita Pizza
**Test:**
1. Create product with base configuration
2. Add size modifiers (Small, Medium, Large)
3. Add topping modifiers (Extra Cheese, Olives, Mushrooms)
4. Verify modifier sync to Talabat
5. Test price calculation with modifiers

---

## Prerequisites & Dependencies

### Required IDs (To be obtained from Foodics API)
- ✅ `CATEGORY_ID` - Get from `/categories` endpoint
- ✅ `TAX_GROUP_ID` - Get from `/tax-groups` endpoint
- ⚠️ `MAIN_DISHES_GROUP_ID` - Create via `/groups` endpoint
- ⚠️ `SALADS_GROUP_ID` - Create via `/groups` endpoint
- ⚠️ `PIZZAS_GROUP_ID` - Create via `/groups` endpoint
- ⚠️ `HEALTHY_OPTIONS_GROUP_ID` - Create via `/groups` endpoint
- ⚠️ `HEALTHY_TAG_ID` - (Optional) Get from `/tags` endpoint
- ⚠️ `POPULAR_TAG_ID` - (Optional) Get from `/tags` endpoint

### API Configuration
```
Base URL: https://api-sandbox.foodics.com/v5
Authentication: Bearer Token
Token: {{token}} (from collection variables)
Headers:
  - Authorization: Bearer {{token}}
  - Accept: application/json
  - Content-Type: application/json
```

---

## Execution Order

1. **Setup Phase**
   ```
   GET /categories → Get existing category ID
   GET /tax-groups → Get existing tax group ID
   GET /groups → Check existing groups
   ```

2. **Create Groups**
   ```
   POST /groups → Create "Main Dishes"
   POST /groups → Create "Salads"
   POST /groups → Create "Pizzas"
   POST /groups → Create "Healthy Options"
   ```

3. **Create Products**
   ```
   POST /products → Create Classic Burger
   POST /products → Create Chicken Caesar Salad
   POST /products → Create Margherita Pizza
   ```

4. **Verification**
   ```
   GET /products → Verify all 3 products created
   GET /products?include=groups → Verify group mappings
   GET /groups?include=products → Verify reverse relationships
   ```

---

## Expected Integration Outcomes

### ✅ Success Criteria
- All 3 products created successfully in Foodics
- Products correctly mapped to their respective groups
- Bilingual content (EN/AR) properly stored
- Products synced to Talabat with correct:
  - Names and descriptions
  - Prices and availability
  - Category mappings
  - Images and metadata

### 🧪 Test Validation Points
1. Product visibility in Talabat menu
2. Correct categorization in Talabat
3. Price consistency between systems
4. Bilingual display support
5. Product active/inactive status sync
6. Preparation time display
7. Calorie information (if supported)

---

## Notes for Integration Development

1. **Price Format:** Foodics uses cents (e.g., 3500 = 35.00 SAR)
2. **Bilingual Support:** Always provide both `name` and `name_localized`
3. **Group Assignment:** Products can belong to multiple groups
4. **Stock Management:** `is_stock_product: false` = non-inventory items
5. **Pricing Method:** 1 = Fixed price per product
6. **Selling Method:** 2 = Sell by unit
7. **Costing Method:** 1 = Fixed cost

---

## Cleanup (Post-Testing)

To clean up test data after integration testing:

```bash
DELETE /products/{CLASSIC_BURGER_ID}
DELETE /products/{CHICKEN_CAESAR_SALAD_ID}
DELETE /products/{MARGHERITA_PIZZA_ID}

DELETE /groups/{MAIN_DISHES_GROUP_ID}
DELETE /groups/{SALADS_GROUP_ID}
DELETE /groups/{PIZZAS_GROUP_ID}
DELETE /groups/{HEALTHY_OPTIONS_GROUP_ID}
```

Or use the Reset endpoint if available:
```bash
POST /reset/orders
POST /reset/inventory
```

---

## Contact & Support

**Foodics API Documentation:** https://docs.foodics.com/
**Foodics Sandbox:** https://api-sandbox.foodics.com/v5
**Collection Last Updated:** 2025-10-30

---

*Generated for Foodics-Talabat Integration Testing*
*Date: 2026-01-29*

