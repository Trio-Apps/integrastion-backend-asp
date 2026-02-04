# 🍽️ Foodics-Talabat Integration Test Data

> **Purpose:** Test data package for validating product and menu synchronization between Foodics POS and Talabat delivery platform

## 📦 Package Contents

This test data package includes everything needed to test the Foodics-Talabat integration:

- ✅ **3 Test Products** (Burger, Salad, Pizza)
- ✅ **4 Menu Groups** (Main Dishes, Salads, Pizzas, Healthy Options)
- ✅ **Automated Scripts** (Creation & Cleanup)
- ✅ **Complete Documentation**

---

## 🚀 Quick Start

### Option 1: Automated (Recommended) ⚡

```powershell
# Create all test data
.\create-test-data.ps1

# After testing, cleanup
.\cleanup-test-data.ps1
```

### Option 2: Using Postman 📮

1. Import Collection ID: `12882056-288945c6-2cf6-4f05-bc30-8cef666bdf74`
2. Run folder: `Groups` → Create all 4 groups
3. Run folder: `Products` → Create all 3 products

### Option 3: Manual API Calls 🔧

See `FOODICS_TEST_DATA_PLAN.md` for detailed API examples

---

## 📊 Test Data Overview

### Products Summary

| Product | Price | Groups | Test Purpose |
|---------|-------|--------|--------------|
| 🍔 Classic Burger | 35 SAR | Main Dishes | Single group mapping |
| 🥗 Caesar Salad | 28 SAR | Salads + Healthy | Multi-group mapping |
| 🍕 Margherita Pizza | 42 SAR | Pizzas | Modifiers support |

### Product-Group Mapping

```
┌─────────────────────┐
│   Main Dishes       │
│  الأطباق الرئيسية   │
└──────┬──────────────┘
       │
       └── Classic Burger (35 SAR)

┌─────────────────────┐
│   Salads            │
│     السلطات         │
└──────┬──────────────┘
       │
       └── Chicken Caesar Salad (28 SAR)

┌─────────────────────┐
│   Pizzas            │
│     البيتزا         │
└──────┬──────────────┘
       │
       └── Margherita Pizza (42 SAR)

┌─────────────────────┐
│  Healthy Options    │
│   خيارات صحية       │
└──────┬──────────────┘
       │
       └── Chicken Caesar Salad (28 SAR)
```

**Note:** Caesar Salad appears in 2 groups to test multi-category mapping!

---

## 📁 File Structure

```
integration-talabat-foodics/
│
├── README.md                      ← You are here
├── FOODICS_TEST_DATA_PLAN.md      ← Detailed documentation
├── TEST_DATA_SUMMARY.md           ← Quick reference guide
│
├── create-test-data.ps1           ← Automated creation script
├── cleanup-test-data.ps1          ← Automated cleanup script
│
├── test-data-payloads.json        ← JSON payloads for manual use
└── test-data-ids.json             ← Generated IDs (after running script)
```

---

## 🎯 Integration Test Scenarios

### 1️⃣ Basic Product Sync
**Product:** Classic Burger  
**Validation:**
- ✅ Product appears on Talabat
- ✅ Correct price (35 SAR)
- ✅ Bilingual name displayed
- ✅ Assigned to "Main Dishes" category

### 2️⃣ Multi-Category Product
**Product:** Chicken Caesar Salad  
**Validation:**
- ✅ Product appears in 2 categories
- ✅ Available in "Salads" section
- ✅ Available in "Healthy Options" section
- ✅ Same product, same price in both

### 3️⃣ Product with Modifiers
**Product:** Margherita Pizza  
**Validation:**
- ✅ Base product syncs correctly
- ✅ Ready for size modifiers (S/M/L)
- ✅ Ready for topping add-ons
- ✅ Price calculations work

---

## 🔑 Key Configuration

### API Details
```
Environment: Sandbox
Base URL: https://api-sandbox.foodics.com/v5
Auth: Bearer Token (pre-configured)
```

### Important Field Formats
- **Price:** In cents (3500 = 35.00 SAR)
- **Names:** Bilingual (EN + AR)
- **SKU:** Unique identifier (TEST-XXXX-001)
- **Groups:** Array of group IDs

---

## ✅ Validation Checklist

After running the creation script:

- [ ] 3 products created
- [ ] 4 groups created
- [ ] Product-group mappings correct
- [ ] `test-data-ids.json` file generated
- [ ] Products visible in Foodics dashboard
- [ ] Products synced to Talabat

---

## 📖 Documentation Files

| File | Purpose | When to Use |
|------|---------|-------------|
| `README.md` | Quick overview | Start here |
| `TEST_DATA_SUMMARY.md` | Quick reference | During testing |
| `FOODICS_TEST_DATA_PLAN.md` | Complete details | Deep dive |
| `test-data-payloads.json` | Raw JSON data | Manual testing |

---

## 🧹 Cleanup

After testing is complete:

```powershell
# Automated cleanup
.\cleanup-test-data.ps1

# Manual verification
# Check Foodics dashboard
# Check Talabat menu
```

---

## 🔍 Troubleshooting

### Problem: Script fails with "Category not found"
**Solution:** 
```powershell
# Get existing categories first
Invoke-RestMethod -Uri "https://api-sandbox.foodics.com/v5/categories" `
  -Headers @{"Authorization"="Bearer YOUR_TOKEN"}
```

### Problem: "Unauthorized" error
**Solution:** Update the token in the script (line 5)

### Problem: Products not appearing on Talabat
**Solution:** 
1. Verify products created in Foodics
2. Check integration sync status
3. Verify products are active (`is_active: true`)
4. Check Talabat webhook logs

---

## 🎓 Learning Resources

- [Foodics API Documentation](https://docs.foodics.com/)
- [Postman Collection](https://www.postman.com/collection/12882056-288945c6-2cf6-4f05-bc30-8cef666bdf74)
- [Integration Guide](./FOODICS_TEST_DATA_PLAN.md)

---

## 📊 Test Data Statistics

| Metric | Value |
|--------|-------|
| Total Products | 3 |
| Total Groups | 4 |
| Total Mappings | 4 |
| Average Price | 35.00 SAR |
| Price Range | 28-42 SAR |
| Average Margin | 53% |

---

## 🔗 API Endpoints Used

```
GET    /categories          → List categories
GET    /tax-groups          → List tax groups
POST   /groups              → Create group
POST   /products            → Create product
GET    /products            → List products
DELETE /products/{id}       → Delete product
DELETE /groups/{id}         → Delete group
```

---

## 📝 Notes

1. **Sandbox Environment:** All data created in sandbox, safe for testing
2. **Bilingual Support:** All names in English and Arabic
3. **Multi-Group:** Caesar Salad intentionally in 2 groups
4. **Price Format:** Cents (multiply by 100)
5. **Cleanup:** Always cleanup after testing

---

## 🤝 Contributing

This test data package is designed to be extended:

- Add more products by editing `test-data-payloads.json`
- Add more groups as needed
- Update test scenarios in `FOODICS_TEST_DATA_PLAN.md`
- Enhance scripts with additional validation

---

## 📞 Support

For issues or questions:
1. Check `FOODICS_TEST_DATA_PLAN.md` for detailed documentation
2. Review `TEST_DATA_SUMMARY.md` for quick reference
3. Consult Foodics API documentation
4. Check integration logs

---

## ⚠️ Important Warnings

1. **Token Security:** Never commit actual tokens to version control
2. **Production:** DO NOT use this in production environment
3. **Cleanup:** Always cleanup test data after testing
4. **IDs:** Replace placeholder IDs with actual ones

---

## 🎉 Success Criteria

Your integration is successful when:

✅ All 3 products visible on Talabat  
✅ Caesar Salad appears in 2 categories  
✅ Prices match exactly  
✅ Bilingual names display correctly  
✅ Product updates sync in real-time  
✅ Inventory status reflects correctly  

---

## 📅 Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2026-01-29 | Initial test data package |

---

**Status:** ✅ Ready for Testing  
**Last Updated:** 2026-01-29  
**Maintained by:** Integration Team

---

*Happy Testing! 🚀*
