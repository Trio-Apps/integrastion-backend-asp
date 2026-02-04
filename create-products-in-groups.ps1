# Foodics Product Creation Script
# This script creates two sample products and assigns them to existing groups
# Generated from Postman Collection Analysis: Foodics Sandbox API
# Save this file as UTF-8 with BOM if you encounter encoding issues

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$PSDefaultParameterValues['*:Encoding'] = 'utf8'

# ============================================
# Configuration
# ============================================

$baseUrl = "https://api-sandbox.foodics.com/v5"
$token = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsImp0aSI6IjQ2NjFkMTFhZDdhYzE4ZWQ0MGM5OTIxMjEzYzAzZDJjNWJiYmMwNjMzODY4ZmM2OGM5OTZhMDE3NjdiNWM1NGI0MDNjNzYyZmQwOWM1NGExIn0.eyJhdWQiOiI5NzQ1M2M3Mi03ZmI4LTQ5YmYtYmUxOC1hYjAwMWI4MWY3NzMiLCJqdGkiOiI0NjYxZDExYWQ3YWMxOGVkNDBjOTkyMTIxM2MwM2QyYzViYmJjMDYzMzg2OGZjNjhjOTk2YTAxNzY3YjVjNTRiNDAzYzc2MmZkMDljNTRhMSIsImlhdCI6MTY4Nzc1NjkzNCwibmJmIjoxNjg3NzU2OTM0LCJleHAiOjE4NDU2MDk3MzQsInN1YiI6Ijk3YmRmOWFlLTA1OWEtNGUzZC04ODIxLWM4MzgxZTQ1MmNiOSIsInNjb3BlcyI6W10sImJ1c2luZXNzIjoiOTdiZGY5YWUtMTRhZC00OTMxLWJiOWQtNWM0ODI0NjQ5NjhjIiwicmVmZXJlbmNlIjoiNjYzMzAwIn0.MRF86GIKzUlaVSWrO95kwwiji_DjML7cHQF0dU6Fh9bF5LW0IqamII_rbVba4GdluPeBrYNf-xxVIrKqyoHhNakBOYX-pT6U9_n7UMlohcy9cQ9OwInAQtQbq9JAdludK3AGd3o4R8T2sMciYhtPQxT3kLCj1VutzQLm7y8p33-_pGaksRWm76ngvkZZ4lFqe8nGHIOCLH74NO8STTIafiTJUyjPRTd2hl31Hwr7F08Vscie9vsbYAYK8QFfuRljaK7Lzx34-jHyIbamounIAqSiC8Z1LFC_r9ZOa3M8YmXiolEwPp2aVmz0z9c9vhcXcFy_56-gB-N_yJhYPtOVd9ev_Q5Ckh5fahrHFSdVaHHhbxpzbuqVJ67rm-Gn_1bPQhhIuOSt41GJAK1qOeEa97sH1qk0M6wK57JffL1UFhr9eJ7oWcz4Qc_jx_BPmrqheOXyc9hRvL9Nk35yYgJhurWyd0hEQmrJWgJcRoUAfHv5Nkgt2OAEG3MCu7vGC3uLe6Ffn7I9Qzb6yQEGj8wxZ1-KaGyhj0Giee-WHg6zedJddLMTGTPfihqrHMKPiG5-qOykPmUp1c8uC45CySFGG6pBkvcFON7rirJ9DRXf-H1xW3eyc1-hq9swSaxYO1ELwFv420QP4ZgPd-Qytli0kx1pb5kmbNnHV1-S0D-ofF4"

# Target Group IDs (using actual groups from Foodics)
# To get available group IDs, run: list-groups.ps1
$targetGroupIds = @(
    "a0f61694-97ae-4c85-8e50-13beea6deefa",  # Main Dishes Test
    "a0f61696-c3e3-4798-bd67-b2c30cd912b9"   # Pizzas Test
)

$headers = @{
    "Authorization" = "Bearer $token"
    "Accept" = "application/json"
    "Content-Type" = "application/json; charset=utf-8"
}

# ============================================
# Helper Functions
# ============================================

function Write-Header {
    param([string]$Message)
    Write-Host ""
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "[STEP] $Message" -ForegroundColor Yellow
    Write-Host ""
}

function Write-Success {
    param([string]$Message)
    Write-Host "  [OK] $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "  [INFO] $Message" -ForegroundColor Gray
}

function Write-Warning {
    param([string]$Message)
    Write-Host "  [WARN] $Message" -ForegroundColor Yellow
}

function Write-ErrorMsg {
    param([string]$Message, [string]$Details = "")
    Write-Host "  [ERROR] $Message" -ForegroundColor Red
    if ($Details) {
        Write-Host "  Details: $Details" -ForegroundColor Red
    }
}

# ============================================
# Main Script
# ============================================

Write-Header "Foodics Product Creation Script"
Write-Host "This script creates 2 sample products assigned to specified groups" -ForegroundColor White
Write-Host "Target Groups: $($targetGroupIds -join ', ')" -ForegroundColor White
Write-Host ""

# ============================================
# Step 1: Test API Connection
# ============================================

Write-Step "Step 1: Testing API Connection"

try {
    Write-Info "Connecting to Foodics API..."
    $testResponse = Invoke-RestMethod -Uri "$baseUrl/categories?filter[is_deleted]=false" -Method GET -Headers $headers -ErrorAction Stop
    Write-Success "API connection successful!"
    Write-Info "API Base URL: $baseUrl"
} catch {
    Write-ErrorMsg "Cannot connect to Foodics API"
    Write-ErrorMsg "Status Code: $($_.Exception.Response.StatusCode.value__)"
    Write-ErrorMsg "Error: $($_.Exception.Message)"
    Write-Host ""
    Write-Warning "Possible issues:"
    Write-Host "    1. Token expired (update line 11 in script)" -ForegroundColor White
    Write-Host "    2. Wrong base URL (currently: $baseUrl)" -ForegroundColor White
    Write-Host "    3. Network/firewall blocking the request" -ForegroundColor White
    Write-Host ""
    Write-Warning "To fix:"
    Write-Host "    - Get a fresh token from Foodics dashboard" -ForegroundColor White
    Write-Host "    - Update the token variable in this script" -ForegroundColor White
    exit 1
}

# ============================================
# Step 2: Fetch Prerequisites
# ============================================

Write-Step "Step 2: Fetching Prerequisites (Category ID)"

try {
    Write-Info "Fetching active categories..."
    $categoriesResponse = Invoke-RestMethod -Uri "$baseUrl/categories?filter[is_deleted]=false" -Method GET -Headers $headers
    
    if ($categoriesResponse.data -and $categoriesResponse.data.Count -gt 0) {
        $categoryId = $categoriesResponse.data[0].id
        Write-Success "Found valid category ID: $categoryId"
        Write-Info "Category name: $($categoriesResponse.data[0].name)"
    } else {
        Write-ErrorMsg "No active categories found. Please create a category first."
        Write-Warning "You can create a category in Foodics dashboard or via API"
        exit 1
    }
    
    Write-Info "Tax group is optional - products work fine without it"
    $taxGroupId = $null
    
} catch {
    Write-ErrorMsg "Error fetching prerequisites: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        Write-ErrorMsg "Response Status: $($_.Exception.Response.StatusCode.value__)"
    }
    exit 1
}

# ============================================
# Step 3: Verify Target Groups Exist
# ============================================

Write-Step "Step 3: Verifying Target Groups"

$verifiedGroups = @()

foreach ($groupId in $targetGroupIds) {
    try {
        Write-Info "Verifying group: $groupId..."
        $groupResponse = Invoke-RestMethod -Uri "$baseUrl/groups/$groupId" -Method GET -Headers $headers -ErrorAction Stop
        
        if ($groupResponse.data) {
            Write-Success "Group verified: $($groupResponse.data.name) (ID: $groupId)"
            $verifiedGroups += @{
                id = $groupId
                name = $groupResponse.data.name
            }
        }
    } catch {
        Write-ErrorMsg "Cannot verify group: $groupId"
        Write-ErrorMsg "Error: $($_.Exception.Message)"
        Write-Warning "This group ID may not exist or is inaccessible"
        Write-Warning "Script will continue, but products may not be assigned to this group"
    }
}

if ($verifiedGroups.Count -eq 0) {
    Write-ErrorMsg "No valid groups found!"
    Write-Warning "Please verify the group IDs are correct"
    exit 1
}

Write-Success "Verified $($verifiedGroups.Count) group(s)"

# ============================================
# Step 4: Create Products
# ============================================

Write-Step "Step 4: Creating Products"

$products = @(
    @{
        key = "premium_steak"
        name = "Premium Ribeye Steak"
        name_localized = $null
        description = "Perfectly grilled premium ribeye steak with seasoned butter and fresh herbs"
        description_localized = $null
        image = "https://example.com/images/ribeye-steak.jpg"
        is_active = $true
        is_stock_product = $false
        pricing_method = 1
        selling_method = 2
        costing_method = 1
        price = 8500
        cost = 4200
        calories = 720
        preparation_time = 25
        sku = "PROD-STEAK-001"
        barcode = "2000000000001"
        category_id = $categoryId
        tax_group_id = $taxGroupId
        tags = @()
        groups = @(@{ id = $targetGroupIds[0] })
    },
    @{
        key = "gourmet_salmon"
        name = "Gourmet Grilled Salmon"
        name_localized = $null
        description = "Fresh Atlantic salmon fillet with lemon butter sauce and steamed vegetables"
        description_localized = $null
        image = "https://example.com/images/grilled-salmon.jpg"
        is_active = $true
        is_stock_product = $false
        pricing_method = 1
        selling_method = 2
        costing_method = 1
        price = 7200
        cost = 3800
        calories = 480
        preparation_time = 20
        sku = "PROD-SALMON-001"
        barcode = "2000000000002"
        category_id = $categoryId
        tax_group_id = $taxGroupId
        tags = @()
        groups = @(@{ id = $targetGroupIds[1] })
    }
)

$createdProducts = @()

foreach ($product in $products) {
    try {
        Write-Info "Creating product: $($product.name)..."
        
        $productData = $product.Clone()
        $key = $productData["key"]
        $productData.Remove("key")
        
        if ($null -eq $productData["tax_group_id"] -or $productData["tax_group_id"] -eq $null) {
            $productData.Remove("tax_group_id")
        }
        
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
        $response = Invoke-RestMethod -Uri "$baseUrl/products" -Method POST -Headers $headers -Body $body -ErrorAction Stop
        
        if ($response.data) {
            $createdProducts += @{
                id = $response.data.id
                name = $product.name
                sku = $product.sku
                price = $product.price
                group_id = $product.groups[0].id
            }
            Write-Success "Created product: $($product.name)"
            Write-Info "Product ID: $($response.data.id)"
            Write-Info "SKU: $($product.sku)"
            Write-Info "Price: $($product.price / 100) (in currency)"
        }
        
    } catch {
        Write-ErrorMsg "Error creating product: $($product.name)"
        Write-ErrorMsg "Error: $($_.Exception.Message)"
        
        if ($_.Exception.Response) {
            try {
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $reader.BaseStream.Position = 0
                $reader.DiscardBufferedData()
                $responseBody = $reader.ReadToEnd()
                Write-ErrorMsg "Response: $responseBody"
            } catch {
                Write-ErrorMsg "Could not read error response"
            }
        }
    }
}

# ============================================
# Step 5: Summary & Save Results
# ============================================

Write-Header "Product Creation Summary"

if ($createdProducts.Count -gt 0) {
    Write-Host "Successfully created $($createdProducts.Count) product(s):" -ForegroundColor Green
    Write-Host ""
    
    foreach ($product in $createdProducts) {
        Write-Host "  Product: $($product.name)" -ForegroundColor White
        Write-Host "    - ID: $($product.id)" -ForegroundColor Gray
        Write-Host "    - SKU: $($product.sku)" -ForegroundColor Gray
        Write-Host "    - Price: $($product.price / 100)" -ForegroundColor Gray
        Write-Host "    - Group: $($product.group_id)" -ForegroundColor Gray
        Write-Host ""
    }
    
    Write-Host "Product-Group Assignments:" -ForegroundColor Yellow
    Write-Host "  - Premium Ribeye Steak -> Group: $($targetGroupIds[0])" -ForegroundColor White
    Write-Host "  - Gourmet Grilled Salmon -> Group: $($targetGroupIds[1])" -ForegroundColor White
    Write-Host ""
    
    $resultsData = @{
        created_at = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        api_base_url = $baseUrl
        category_id = $categoryId
        target_groups = $targetGroupIds
        verified_groups = $verifiedGroups
        products = $createdProducts
        summary = @{
            total_products_created = $createdProducts.Count
            total_groups_targeted = $targetGroupIds.Count
            total_groups_verified = $verifiedGroups.Count
        }
    }
    
    $outputFile = "product-creation-results.json"
    $resultsData | ConvertTo-Json -Depth 10 | Out-File -FilePath $outputFile -Encoding UTF8
    
    Write-Success "Results saved to: $outputFile"
    Write-Host ""
    Write-Host "Script completed successfully!" -ForegroundColor Green
    
} else {
    Write-ErrorMsg "No products were created!"
    Write-Warning "Please check the errors above and try again"
    exit 1
}

Write-Header "Task Complete"

