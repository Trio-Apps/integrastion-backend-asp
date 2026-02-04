# Foodics Test Data Creation Script
# This script creates test products and groups for Foodics-Talabat integration testing
# Save this file as UTF-8 with BOM if you encounter encoding issues

$baseUrl = "https://api-sandbox.foodics.com/v5"
$token = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsImp0aSI6IjQ2NjFkMTFhZDdhYzE4ZWQ0MGM5OTIxMjEzYzAzZDJjNWJiYmMwNjMzODY4ZmM2OGM5OTZhMDE3NjdiNWM1NGI0MDNjNzYyZmQwOWM1NGExIn0.eyJhdWQiOiI5NzQ1M2M3Mi03ZmI4LTQ5YmYtYmUxOC1hYjAwMWI4MWY3NzMiLCJqdGkiOiI0NjYxZDExYWQ3YWMxOGVkNDBjOTkyMTIxM2MwM2QyYzViYmJjMDYzMzg2OGZjNjhjOTk2YTAxNzY3YjVjNTRiNDAzYzc2MmZkMDljNTRhMSIsImlhdCI6MTY4Nzc1NjkzNCwibmJmIjoxNjg3NzU2OTM0LCJleHAiOjE4NDU2MDk3MzQsInN1YiI6Ijk3YmRmOWFlLTA1OWEtNGUzZC04ODIxLWM4MzgxZTQ1MmNiOSIsInNjb3BlcyI6W10sImJ1c2luZXNzIjoiOTdiZGY5YWUtMTRhZC00OTMxLWJiOWQtNWM0ODI0NjQ5NjhjIiwicmVmZXJlbmNlIjoiNjYzMzAwIn0.MRF86GIKzUlaVSWrO95kwwiji_DjML7cHQF0dU6Fh9bF5LW0IqamII_rbVba4GdluPeBrYNf-xxVIrKqyoHhNakBOYX-pT6U9_n7UMlohcy9cQ9OwInAQtQbq9JAdludK3AGd3o4R8T2sMciYhtPQxT3kLCj1VutzQLm7y8p33-_pGaksRWm76ngvkZZ4lFqe8nGHIOCLH74NO8STTIafiTJUyjPRTd2hl31Hwr7F08Vscie9vsbYAYK8QFfuRljaK7Lzx34-jHyIbamounIAqSiC8Z1LFC_r9ZOa3M8YmXiolEwPp2aVmz0z9c9vhcXcFy_56-gB-N_yJhYPtOVd9ev_Q5Ckh5fahrHFSdVaHHhbxpzbuqVJ67rm-Gn_1bPQhhIuOSt41GJAK1qOeEa97sH1qk0M6wK57JffL1UFhr9eJ7oWcz4Qc_jx_BPmrqheOXyc9hRvL9Nk35yYgJhurWyd0hEQmrJWgJcRoUAfHv5Nkgt2OAEG3MCu7vGC3uLe6Ffn7I9Qzb6yQEGj8wxZ1-KaGyhj0Giee-WHg6zedJddLMTGTPfihqrHMKPiG5-qOykPmUp1c8uC45CySFGG6pBkvcFON7rirJ9DRXf-H1xW3eyc1-hq9swSaxYO1ELwFv420QP4ZgPd-Qytli0kx1pb5kmbNnHV1-S0D-ofF4"

$headers = @{
    "Authorization" = "Bearer $token"
    "Accept" = "application/json"
    "Content-Type" = "application/json; charset=utf-8"
}

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$PSDefaultParameterValues['*:Encoding'] = 'utf8'

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Foodics Test Data Creation Script" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Get existing categories and tax groups
Write-Host "[Step 1] Fetching existing categories and tax groups..." -ForegroundColor Yellow

# Test API connection first
try {
    Write-Host "  Testing API connection..." -ForegroundColor Gray
    $testResponse = Invoke-RestMethod -Uri "$baseUrl/categories" -Method GET -Headers $headers -ErrorAction Stop
    Write-Host "  API connection successful!" -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] Cannot connect to Foodics API" -ForegroundColor Red
    Write-Host "  Status Code: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "" -ForegroundColor Yellow
    Write-Host "  Possible issues:" -ForegroundColor Yellow
    Write-Host "    1. Token expired (check line 5 in script)" -ForegroundColor White
    Write-Host "    2. Wrong base URL (currently: $baseUrl)" -ForegroundColor White
    Write-Host "    3. Network/firewall blocking the request" -ForegroundColor White
    Write-Host "" -ForegroundColor Yellow
    Write-Host "  To fix:" -ForegroundColor Yellow
    Write-Host "    - Get a fresh token from Foodics dashboard" -ForegroundColor White
    Write-Host "    - Update line 5 in this script with the new token" -ForegroundColor White
    exit 1
}

try {
    # Get non-deleted categories only
    $categoriesResponse = Invoke-RestMethod -Uri "$baseUrl/categories?filter[is_deleted]=false" -Method GET -Headers $headers
    
    if ($categoriesResponse.data -and $categoriesResponse.data.Count -gt 0) {
        $categoryId = $categoriesResponse.data[0].id
        Write-Host "  [OK] Found valid category ID: $categoryId" -ForegroundColor Green
        Write-Host "      Category name: $($categoriesResponse.data[0].name)" -ForegroundColor Gray
    } else {
        Write-Host "  [ERROR] No active categories found. Please create a category first." -ForegroundColor Red
        Write-Host "  You can create a category in Foodics dashboard or via API" -ForegroundColor Yellow
        exit 1
    }
    
    # Try to get taxes (completely optional - products work fine without tax)
    $taxGroupId = $null
    Write-Host "  [INFO] Skipping tax group (optional for products)" -ForegroundColor Yellow
    
} catch {
    Write-Host "  [ERROR] Error fetching prerequisites: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        Write-Host "  Response Status: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Red
    }
    exit 1
}

Write-Host ""

# Step 2: Create Groups
Write-Host "[Step 2] Creating test groups..." -ForegroundColor Yellow

$groups = @{
    "main_dishes" = @{
        name = "Main Dishes Test"
        name_localized = $null
        image = "https://example.com/images/main-dishes.jpg"
        items_index = 1
        is_active = $true
        products = @()
    }
    "salads" = @{
        name = "Salads Test"
        name_localized = $null
        image = "https://example.com/images/salads.jpg"
        items_index = 2
        is_active = $true
        products = @()
    }
    "pizzas" = @{
        name = "Pizzas Test"
        name_localized = $null
        image = "https://example.com/images/pizzas.jpg"
        items_index = 3
        is_active = $true
        products = @()
    }
    "healthy_options" = @{
        name = "Healthy Options Test"
        name_localized = $null
        image = "https://example.com/images/healthy-options.jpg"
        items_index = 4
        is_active = $true
        products = @()
    }
}

$groupIds = @{}

foreach ($groupKey in $groups.Keys) {
    try {
        $body = $groups[$groupKey] | ConvertTo-Json -Depth 10
        $response = Invoke-RestMethod -Uri "$baseUrl/groups" -Method POST -Headers $headers -Body $body
        $groupIds[$groupKey] = $response.data.id
        Write-Host "  [OK] Created group: $($groups[$groupKey].name) (ID: $($response.data.id))" -ForegroundColor Green
    } catch {
        Write-Host "  [ERROR] Error creating group $($groups[$groupKey].name): $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""

# Step 3: Create Products
Write-Host "[Step 3] Creating test products..." -ForegroundColor Yellow

$products = @(
    @{
        key = "classic_burger"
        name = "Classic Burger Test"
        name_localized = $null
        description = "Juicy beef patty with lettuce tomato and special sauce"
        description_localized = $null
        image = "https://example.com/images/classic-burger.jpg"
        is_active = $true
        is_stock_product = $false
        pricing_method = 1
        selling_method = 2
        costing_method = 1
        price = 3500
        cost = 1800
        calories = 650
        preparation_time = 15
        sku = "TEST-BURGER-001"
        barcode = "1234567890001"
        category_id = $categoryId
        tax_group_id = $taxGroupId
        tags = @()
        groups = @(@{ id = $groupIds["main_dishes"] })
    },
    @{
        key = "chicken_caesar_salad"
        name = "Chicken Caesar Salad Test"
        name_localized = $null
        description = "Fresh romaine lettuce with grilled chicken croutons and Caesar dressing"
        description_localized = $null
        image = "https://example.com/images/chicken-caesar-salad.jpg"
        is_active = $true
        is_stock_product = $false
        pricing_method = 1
        selling_method = 2
        costing_method = 1
        price = 2800
        cost = 1200
        calories = 420
        preparation_time = 10
        sku = "TEST-SALAD-001"
        barcode = "1234567890002"
        category_id = $categoryId
        tax_group_id = $taxGroupId
        tags = @()
        groups = @(
            @{ id = $groupIds["salads"] },
            @{ id = $groupIds["healthy_options"] }
        )
    },
    @{
        key = "margherita_pizza"
        name = "Margherita Pizza Test"
        name_localized = $null
        description = "Classic Italian pizza with tomato sauce mozzarella and fresh basil"
        description_localized = $null
        image = "https://example.com/images/margherita-pizza.jpg"
        is_active = $true
        is_stock_product = $false
        pricing_method = 1
        selling_method = 2
        costing_method = 1
        price = 4200
        cost = 2000
        calories = 850
        preparation_time = 20
        sku = "TEST-PIZZA-001"
        barcode = "1234567890003"
        category_id = $categoryId
        tax_group_id = $taxGroupId
        tags = @()
        groups = @(@{ id = $groupIds["pizzas"] })
    }
)

$productIds = @{}

foreach ($product in $products) {
    try {
        $productData = $product.Clone()
        $key = $productData["key"]
        $productData.Remove("key")
        
        # Remove tax_group_id if it's null
        if ($null -eq $productData["tax_group_id"] -or $productData["tax_group_id"] -eq $null) {
            $productData.Remove("tax_group_id")
        }
        
        # Remove null fields
        $keysToRemove = @()
        foreach ($k in $productData.Keys) {
            if ($null -eq $productData[$k]) {
                $keysToRemove += $k
            }
        }
        foreach ($k in $keysToRemove) {
            $productData.Remove($k)
        }
        
        $body = $productData | ConvertTo-Json -Depth 10
        $response = Invoke-RestMethod -Uri "$baseUrl/products" -Method POST -Headers $headers -Body $body
        $productIds[$key] = $response.data.id
        Write-Host "  [OK] Created product: $($product.name) (ID: $($response.data.id))" -ForegroundColor Green
    } catch {
        Write-Host "  [ERROR] Error creating product $($product.name): $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
            try {
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $reader.BaseStream.Position = 0
                $reader.DiscardBufferedData()
                $responseBody = $reader.ReadToEnd()
                Write-Host "  Response: $responseBody" -ForegroundColor Red
            } catch {
                Write-Host "  Could not read error response" -ForegroundColor Red
            }
        }
    }
}

Write-Host ""

# Step 4: Summary
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Test Data Creation Summary" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Groups Created:" -ForegroundColor Yellow
foreach ($key in $groupIds.Keys) {
    Write-Host "  - $($groups[$key].name): $($groupIds[$key])" -ForegroundColor White
}

Write-Host ""
Write-Host "Products Created:" -ForegroundColor Yellow
foreach ($key in $productIds.Keys) {
    $product = $products | Where-Object { $_.key -eq $key }
    Write-Host "  - $($product.name): $($productIds[$key])" -ForegroundColor White
}

Write-Host ""
Write-Host "Product-Group Mapping:" -ForegroundColor Yellow
Write-Host "  - Classic Burger -> Main Dishes" -ForegroundColor White
Write-Host "  - Chicken Caesar Salad -> Salads, Healthy Options" -ForegroundColor White
Write-Host "  - Margherita Pizza -> Pizzas" -ForegroundColor White

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Test data creation completed!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Save IDs to file for reference
$testDataIds = @{
    category_id = $categoryId
    tax_group_id = $taxGroupId
    groups = $groupIds
    products = $productIds
    created_at = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
}

$testDataIds | ConvertTo-Json -Depth 10 | Out-File -FilePath "test-data-ids.json" -Encoding UTF8
Write-Host "Test data IDs saved to: test-data-ids.json" -ForegroundColor Cyan
Write-Host ""

