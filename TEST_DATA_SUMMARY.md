# Foodics-Talabat Integration Test Data Summary

## 📋 Quick Reference

### Test Products Overview

| # | Product | SKU | Price | Category | Groups | Status |
|---|---------|-----|-------|----------|--------|--------|
| 1 | Classic Burger 🍔 | TEST-BURGER-001 | 35.00 SAR | Main Dishes | Main Dishes | ✅ Ready |
| 2 | Chicken Caesar Salad 🥗 | TEST-SALAD-001 | 28.00 SAR | Salads | Salads, Healthy Options | ✅ Ready |
| 3 | Margherita Pizza 🍕 | TEST-PIZZA-001 | 42.00 SAR | Pizzas | Pizzas | ✅ Ready |

### Test Groups (Menu Categories)

| # | Group Name | Arabic Name | Products Count |
|---|------------|-------------|----------------|
| 1 | Main Dishes | الأطباق الرئيسية | 1 |
| 2 | Salads | السلطات | 1 |
| 3 | Pizzas | البيتزا | 1 |
| 4 | Healthy Options | خيارات صحية | 1 |

---

## 🎯 Test Scenarios

### Scenario 1: Basic Product Sync
- **Product:** Classic Burger
- **Test Type:** Single group mapping
- **Expected:** Product appears in "Main Dishes" on Talabat

### Scenario 2: Multi-Group Product
- **Product:** Chicken Caesar Salad
- **Test Type:** Multiple group associations
- **Expected:** Product appears in both "Salads" AND "Healthy Options" on Talabat

### Scenario 3: Product with Future Modifiers
- **Product:** Margherita Pizza
- **Test Type:** Base product for modifiers testing
- **Expected:** Product sync with potential for size/topping variations

---

## 🚀 Quick Start Guide

### Option 1: Using PowerShell Script (Recommended)
```powershell
# Run the automated script
.\create-test-data.ps1

# Check the output file
cat test-data-ids.json
```

### Option 2: Using Postman Collection
1. Open Postman
2. Import collection ID: `12882056-288945c6-2cf6-4f05-bc30-8cef666bdf74`
3. Run requests in this order:
   - Create groups (4 requests)
   - Create products (3 requests)

### Option 3: Manual API Calls
```bash
# Example: Create a group
curl -X POST https://api-sandbox.foodics.com/v5/groups \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Main Dishes",
    "name_localized": "الأطباق الرئيسية",
    "is_active": true
  }'

# Example: Create a product
curl -X POST https://api-sandbox.foodics.com/v5/products \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Classic Burger",
    "name_localized": "برغر كلاسيك",
    "price": 3500,
    "cost": 1800,
    "sku": "TEST-BURGER-001",
    "category_id": "YOUR_CATEGORY_ID",
    "groups": [{"id": "YOUR_GROUP_ID"}]
  }'
```

---

## 📊 Product Details

### Product 1: Classic Burger

```
Name: Classic Burger / برغر كلاسيك
SKU: TEST-BURGER-001
Barcode: 1234567890001
Price: 35.00 SAR (3500 cents)
Cost: 18.00 SAR (1800 cents)
Margin: 51.4%
Calories: 650 kcal
Prep Time: 15 minutes
Groups: Main Dishes
```

**Test Focus:** Basic single-group product synchronization

---

### Product 2: Chicken Caesar Salad

```
Name: Chicken Caesar Salad / سلطة سيزر بالدجاج
SKU: TEST-SALAD-001
Barcode: 1234567890002
Price: 28.00 SAR (2800 cents)
Cost: 12.00 SAR (1200 cents)
Margin: 57.1%
Calories: 420 kcal
Prep Time: 10 minutes
Groups: Salads, Healthy Options (Multiple!)
```

**Test Focus:** Multi-category product mapping

---

### Product 3: Margherita Pizza

```
Name: Margherita Pizza / بيتزا مارغريتا
SKU: TEST-PIZZA-001
Barcode: 1234567890003
Price: 42.00 SAR (4200 cents)
Cost: 20.00 SAR (2000 cents)
Margin: 52.4%
Calories: 850 kcal
Prep Time: 20 minutes
Groups: Pizzas
```

**Test Focus:** Product with potential modifiers (size, toppings)

---

## 🔗 Product-Group Relationships

```
Main Dishes
└── Classic Burger

Salads
└── Chicken Caesar Salad

Pizzas
└── Margherita Pizza

Healthy Options
└── Chicken Caesar Salad
```

**Note:** Chicken Caesar Salad intentionally belongs to 2 groups to test multi-category mapping.

---

## ✅ Validation Checklist

After creating test data, verify:

- [ ] All 4 groups created successfully
- [ ] All 3 products created successfully
- [ ] Classic Burger linked to Main Dishes group
- [ ] Chicken Caesar Salad linked to 2 groups
- [ ] Margherita Pizza linked to Pizzas group
- [ ] All products have correct prices
- [ ] All products have bilingual names
- [ ] All products are marked as active (`is_active: true`)

---

## 🔧 Integration Testing Steps

### Phase 1: Setup
1. ✅ Create test groups in Foodics
2. ✅ Create test products in Foodics
3. ✅ Verify product-group mappings

### Phase 2: Integration
4. ⏳ Trigger sync to Talabat
5. ⏳ Verify products appear on Talabat
6. ⏳ Check category mappings
7. ⏳ Validate prices and descriptions

### Phase 3: Validation
8. ⏳ Test product visibility
9. ⏳ Test multi-category product
10. ⏳ Test bilingual display
11. ⏳ Test product updates sync

### Phase 4: Cleanup
12. ⏳ Remove test products
13. ⏳ Remove test groups
14. ⏳ Verify cleanup on both systems

---

## 📝 Key Field Mappings

| Foodics Field | Value Type | Example | Notes |
|---------------|------------|---------|-------|
| `price` | Integer (cents) | 3500 | 35.00 SAR |
| `cost` | Integer (cents) | 1800 | 18.00 SAR |
| `pricing_method` | Integer | 1 | 1 = Fixed price |
| `selling_method` | Integer | 2 | 2 = By unit |
| `costing_method` | Integer | 1 | 1 = Fixed cost |
| `is_active` | Boolean | true | Product available |
| `is_stock_product` | Boolean | false | No inventory tracking |
| `preparation_time` | Integer | 15 | Minutes |
| `calories` | Integer | 650 | kcal |

---

## 🗑️ Cleanup Commands

### Using PowerShell Script
```powershell
# Run the cleanup script
.\cleanup-test-data.ps1
```

### Manual Cleanup
```bash
# Delete products
DELETE /products/{PRODUCT_ID}

# Delete groups
DELETE /groups/{GROUP_ID}
```

---

## 📄 Files in This Test Package

| File | Purpose |
|------|---------|
| `FOODICS_TEST_DATA_PLAN.md` | Comprehensive test plan and documentation |
| `TEST_DATA_SUMMARY.md` | This file - quick reference guide |
| `test-data-payloads.json` | JSON payloads for manual testing |
| `create-test-data.ps1` | PowerShell script to create all test data |
| `cleanup-test-data.ps1` | PowerShell script to remove test data |
| `test-data-ids.json` | Generated file with created resource IDs |

---

## 🔐 API Configuration

```
Base URL: https://api-sandbox.foodics.com/v5
Token: (Stored in collection)
Format: Bearer Token

Required Headers:
- Authorization: Bearer {{token}}
- Accept: application/json
- Content-Type: application/json
```

---

## 💡 Tips & Best Practices

1. **Price Format:** Always use cents (3500 = 35.00 SAR)
2. **Bilingual:** Provide both English and Arabic names
3. **Categories:** Products must have a valid category_id
4. **Tax Groups:** Products should have a valid tax_group_id
5. **Groups:** Products can belong to multiple groups
6. **SKU:** Must be unique across all products
7. **Barcode:** Optional but recommended for POS integration
8. **Images:** Use placeholder URLs for testing

---

## 🆘 Troubleshooting

### Problem: "Category ID not found"
**Solution:** Get category ID first:
```bash
GET /categories
```

### Problem: "Tax group ID not found"
**Solution:** Get tax group ID first:
```bash
GET /tax-groups
```

### Problem: "Group ID not found"
**Solution:** Create groups before products

### Problem: "SKU already exists"
**Solution:** Use unique SKU or delete existing product

---

## 📞 Support Resources

- **Foodics API Docs:** https://docs.foodics.com/
- **Postman Collection:** ID `12882056-288945c6-2cf6-4f05-bc30-8cef666bdf74`
- **Sandbox Environment:** https://api-sandbox.foodics.com/v5

---

**Generated:** 2026-01-29
**Last Updated:** 2026-01-29
**Status:** ✅ Ready for Testing

