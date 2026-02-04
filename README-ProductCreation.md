# Foodics Product Creation Scripts

## Overview

Professional PowerShell scripts for creating and managing products in the Foodics Sandbox API, analyzed and generated using the Postman MCP server.

## 📋 Scripts Included

### 1. `create-products-in-groups.ps1`
**Purpose:** Creates two sample products and assigns them to specified Foodics groups.

**Features:**
- ✅ API connection validation
- ✅ Automatic category ID fetching
- ✅ Group existence verification
- ✅ Product creation with full details
- ✅ Comprehensive error handling
- ✅ Results saved to JSON file
- ✅ Color-coded console output

**Usage:**
```powershell
powershell -ExecutionPolicy Bypass -File create-products-in-groups.ps1
```

### 2. `list-groups.ps1`
**Purpose:** Lists all available groups in your Foodics environment.

**Usage:**
```powershell
powershell -ExecutionPolicy Bypass -File list-groups.ps1
```

### 3. `create-test-data.ps1`
**Purpose:** Original script that creates test groups and products (reference).

## 🎯 Postman Collection Analysis

**Collection Analyzed:** Foodics Sandbox  
**Collection ID:** `12882056-288945c6-2cf6-4f05-bc30-8cef666bdf74`

### Key Endpoints Used:

1. **List Categories** (GET `/categories`)
   - Used to fetch category IDs required for product creation
   - Filter: `is_deleted=false` for active categories only

2. **List Groups** (GET `/groups`)
   - Lists all menu groups
   - Filter: `is_deleted=false` for active groups only

3. **Get Group** (GET `/groups/{id}`)
   - Verifies group existence before product creation

4. **Create Product** (POST `/products`)
   - Creates products with full specifications
   - Required fields: name, category_id, pricing_method, selling_method
   - Optional: description, image, groups assignment

## 📊 Execution Results

### Latest Run (2026-01-31 19:08:20)

**Products Created:**

| Product Name | ID | SKU | Price | Group |
|-------------|-----|-----|-------|-------|
| Premium Ribeye Steak | `a0f8213c-ff70-44bc-a90e-08cb78e1deff` | PROD-STEAK-001 | 85.00 | Main Dishes Test |
| Gourmet Grilled Salmon | `a0f8213e-b32b-4652-b5be-56d31f63eefd` | PROD-SALMON-001 | 72.00 | Pizzas Test |

**Groups Verified:**
- Main Dishes Test (`a0f61694-97ae-4c85-8e50-13beea6deefa`)
- Pizzas Test (`a0f61696-c3e3-4798-bd67-b2c30cd912b9`)

**Category Used:**
- 🎁 PARTY PACK (`a0ebc18f-7d17-4567-a621-c1ba47f5b234`)

## 🔧 Configuration

### Update Authentication Token

If you get authentication errors, update the token in the scripts:

```powershell
$token = "YOUR_NEW_TOKEN_HERE"
```

To get a fresh token:
1. Log in to [Foodics Dashboard](https://console.foodics.com/)
2. Navigate to Settings → API Tokens
3. Generate a new token with required permissions
4. Replace the token in the script

### Customize Group IDs

To use different groups, run `list-groups.ps1` first to see available groups:

```powershell
powershell -ExecutionPolicy Bypass -File list-groups.ps1
```

Then update the `$targetGroupIds` array in `create-products-in-groups.ps1`:

```powershell
$targetGroupIds = @(
    "your-first-group-id",
    "your-second-group-id"
)
```

## 📝 Product Schema

Products are created with the following structure:

```json
{
  "name": "Product Name",
  "description": "Product description",
  "image": "https://example.com/image.jpg",
  "is_active": true,
  "is_stock_product": false,
  "pricing_method": 1,
  "selling_method": 2,
  "costing_method": 1,
  "price": 8500,
  "cost": 4200,
  "calories": 720,
  "preparation_time": 25,
  "sku": "UNIQUE-SKU",
  "barcode": "1234567890",
  "category_id": "category-uuid",
  "groups": [
    { "id": "group-uuid" }
  ]
}
```

### Field Descriptions:

- **pricing_method**: `1` = Fixed price
- **selling_method**: `2` = Per unit
- **costing_method**: `1` = Fixed cost
- **price**: Price in smallest currency unit (e.g., cents)
- **cost**: Cost in smallest currency unit
- **calories**: Nutritional information (optional)
- **preparation_time**: Time in minutes
- **is_stock_product**: `false` = not tracked in inventory

## 🛠️ Troubleshooting

### Error: "Token expired"
**Solution:** Update the `$token` variable with a fresh token from Foodics dashboard.

### Error: "Group not found (404)"
**Solution:** Run `list-groups.ps1` to get valid group IDs, then update `$targetGroupIds`.

### Error: "No active categories found"
**Solution:** Create a category in Foodics dashboard first.

### Error: "Cannot connect to Foodics API"
**Possible causes:**
1. Internet connection issues
2. Firewall blocking the request
3. Incorrect base URL
4. Token authentication failed

## 📦 Output Files

### `product-creation-results.json`
Contains complete details of the creation session:
- All created product IDs, names, SKUs, prices
- Verified group information
- Category used
- Timestamp
- Summary statistics

**Example:**
```json
{
  "created_at": "2026-01-31 19:08:20",
  "api_base_url": "https://api-sandbox.foodics.com/v5",
  "category_id": "a0ebc18f-7d17-4567-a621-c1ba47f5b234",
  "products": [
    {
      "id": "a0f8213c-ff70-44bc-a90e-08cb78e1deff",
      "name": "Premium Ribeye Steak",
      "sku": "PROD-STEAK-001",
      "price": 8500,
      "group_id": "a0f61694-97ae-4c85-8e50-13beea6deefa"
    }
  ],
  "summary": {
    "total_products_created": 2,
    "total_groups_targeted": 2,
    "total_groups_verified": 2
  }
}
```

## 🎓 Best Practices

1. **Always validate group IDs** before running the script
2. **Use meaningful SKUs** for easy product identification
3. **Keep tokens secure** - never commit to version control
4. **Review output** in console for any warnings or errors
5. **Save result files** for audit and reference purposes

## 🔒 Security Notes

⚠️ **IMPORTANT:** This script contains API tokens that grant access to your Foodics account.

- **Never** commit tokens to version control (Git, etc.)
- **Rotate tokens** regularly
- **Use environment variables** for production environments
- **Limit token permissions** to only what's needed

### Recommended: Use Environment Variables

```powershell
# Set in your PowerShell profile or session
$env:FOODICS_TOKEN = "your-token-here"

# Then in script:
$token = $env:FOODICS_TOKEN
```

## 📚 API Documentation

- [Foodics API Documentation](https://api.foodics.com/docs/)
- [Postman Collection Format](https://schema.postman.com/collection/json/v2.1.0/draft-07/docs/index.html)

## 🚀 Next Steps

1. ✅ **Customize products** - Edit product details in the script
2. ✅ **Add more products** - Extend the `$products` array
3. ✅ **Integrate with CI/CD** - Automate product creation
4. ✅ **Add modifiers** - Attach product modifiers via API
5. ✅ **Set pricing rules** - Use price tags for dynamic pricing

## 📄 License

Created for internal use with Foodics-Talabat integration project.

---

**Last Updated:** 2026-01-31  
**Author:** Senior Software Engineer  
**API Version:** Foodics API v5


