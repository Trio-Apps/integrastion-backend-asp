# Foodics Test Data Cleanup Script
# This script removes all test products and groups created for Foodics-Talabat integration testing

$baseUrl = "https://api-sandbox.foodics.com/v5"
$token = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsImp0aSI6IjQ2NjFkMTFhZDdhYzE4ZWQ0MGM5OTIxMjEzYzAzZDJjNWJiYmMwNjMzODY4ZmM2OGM5OTZhMDE3NjdiNWM1NGI0MDNjNzYyZmQwOWM1NGExIn0.eyJhdWQiOiI5NzQ1M2M3Mi03ZmI4LTQ5YmYtYmUxOC1hYjAwMWI4MWY3NzMiLCJqdGkiOiI0NjYxZDExYWQ3YWMxOGVkNDBjOTkyMTIxM2MwM2QyYzViYmJjMDYzMzg2OGZjNjhjOTk2YTAxNzY3YjVjNTRiNDAzYzc2MmZkMDljNTRhMSIsImlhdCI6MTY4Nzc1NjkzNCwibmJmIjoxNjg3NzU2OTM0LCJleHAiOjE4NDU2MDk3MzQsInN1YiI6Ijk3YmRmOWFlLTA1OWEtNGUzZC04ODIxLWM4MzgxZTQ1MmNiOSIsInNjb3BlcyI6W10sImJ1c2luZXNzIjoiOTdiZGY5YWUtMTRhZC00OTMxLWJiOWQtNWM0ODI0NjQ5NjhjIiwicmVmZXJlbmNlIjoiNjYzMzAwIn0.MRF86GIKzUlaVSWrO95kwwiji_DjML7cHQF0dU6Fh9bF5LW0IqamII_rbVba4GdluPeBrYNf-xxVIrKqyoHhNakBOYX-pT6U9_n7UMlohcy9cQ9OwInAQtQbq9JAdludK3AGd3o4R8T2sMciYhtPQxT3kLCj1VutzQLm7y8p33-_pGaksRWm76ngvkZZ4lFqe8nGHIOCLH74NO8STTIafiTJUyjPRTd2hl31Hwr7F08Vscie9vsbYAYK8QFfuRljaK7Lzx34-jHyIbamounIAqSiC8Z1LFC_r9ZOa3M8YmXiolEwPp2aVmz0z9c9vhcXcFy_56-gB-N_yJhYPtOVd9ev_Q5Ckh5fahrHFSdVaHHhbxpzbuqVJ67rm-Gn_1bPQhhIuOSt41GJAK1qOeEa97sH1qk0M6wK57JffL1UFhr9eJ7oWcz4Qc_jx_BPmrqheOXyc9hRvL9Nk35yYgJhurWyd0hEQmrJWgJcRoUAfHv5Nkgt2OAEG3MCu7vGC3uLe6Ffn7I9Qzb6yQEGj8wxZ1-KaGyhj0Giee-WHg6zedJddLMTGTPfihqrHMKPiG5-qOykPmUp1c8uC45CySFGG6pBkvcFON7rirJ9DRXf-H1xW3eyc1-hq9swSaxYO1ELwFv420QP4ZgPd-Qytli0kx1pb5kmbNnHV1-S0D-ofF4"

$headers = @{
    "Authorization" = "Bearer $token"
    "Accept" = "application/json"
    "Content-Type" = "application/json"
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Foodics Test Data Cleanup Script" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Check if test-data-ids.json exists
if (Test-Path "test-data-ids.json") {
    Write-Host "[INFO] Found test-data-ids.json file" -ForegroundColor Green
    $testDataIds = Get-Content "test-data-ids.json" | ConvertFrom-Json
    
    Write-Host ""
    Write-Host "[Step 1] Deleting test products..." -ForegroundColor Yellow
    
    if ($testDataIds.products) {
        foreach ($productKey in $testDataIds.products.PSObject.Properties.Name) {
            $productId = $testDataIds.products.$productKey
            try {
                Invoke-RestMethod -Uri "$baseUrl/products/$productId" -Method DELETE -Headers $headers
                Write-Host "  ✓ Deleted product ID: $productId" -ForegroundColor Green
            } catch {
                Write-Host "  ✗ Error deleting product $productId : $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    } else {
        Write-Host "  ℹ No products found in test-data-ids.json" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "[Step 2] Deleting test groups..." -ForegroundColor Yellow
    
    if ($testDataIds.groups) {
        foreach ($groupKey in $testDataIds.groups.PSObject.Properties.Name) {
            $groupId = $testDataIds.groups.$groupKey
            try {
                Invoke-RestMethod -Uri "$baseUrl/groups/$groupId" -Method DELETE -Headers $headers
                Write-Host "  ✓ Deleted group ID: $groupId" -ForegroundColor Green
            } catch {
                Write-Host "  ✗ Error deleting group $groupId : $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    } else {
        Write-Host "  ℹ No groups found in test-data-ids.json" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "✓ Cleanup completed!" -ForegroundColor Green
    
    # Optionally remove the test-data-ids.json file
    $removeFile = Read-Host "Do you want to delete test-data-ids.json? (Y/N)"
    if ($removeFile -eq "Y" -or $removeFile -eq "y") {
        Remove-Item "test-data-ids.json"
        Write-Host "  ✓ Removed test-data-ids.json" -ForegroundColor Green
    }
    
} else {
    Write-Host "[WARNING] test-data-ids.json not found!" -ForegroundColor Red
    Write-Host "This file is created by create-test-data.ps1" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Manual cleanup options:" -ForegroundColor Yellow
    Write-Host "1. List products by SKU and delete manually" -ForegroundColor White
    Write-Host "2. Use Postman collection to find and delete test products" -ForegroundColor White
    Write-Host ""
    
    $manualCleanup = Read-Host "Do you want to search and cleanup by SKU pattern? (Y/N)"
    
    if ($manualCleanup -eq "Y" -or $manualCleanup -eq "y") {
        Write-Host ""
        Write-Host "[Step 1] Searching for test products by SKU..." -ForegroundColor Yellow
        
        $testSkus = @("TEST-BURGER-001", "TEST-SALAD-001", "TEST-PIZZA-001")
        $foundProducts = @()
        
        foreach ($sku in $testSkus) {
            try {
                $response = Invoke-RestMethod -Uri "$baseUrl/products?filter[sku]=$sku" -Method GET -Headers $headers
                if ($response.data -and $response.data.Count -gt 0) {
                    $foundProducts += $response.data[0]
                    Write-Host "  ✓ Found product: $($response.data[0].name) (ID: $($response.data[0].id))" -ForegroundColor Green
                }
            } catch {
                Write-Host "  ✗ Error searching for SKU $sku : $($_.Exception.Message)" -ForegroundColor Red
            }
        }
        
        if ($foundProducts.Count -gt 0) {
            Write-Host ""
            Write-Host "[Step 2] Deleting found products..." -ForegroundColor Yellow
            
            foreach ($product in $foundProducts) {
                try {
                    Invoke-RestMethod -Uri "$baseUrl/products/$($product.id)" -Method DELETE -Headers $headers
                    Write-Host "  ✓ Deleted product: $($product.name) (ID: $($product.id))" -ForegroundColor Green
                } catch {
                    Write-Host "  ✗ Error deleting product $($product.id) : $($_.Exception.Message)" -ForegroundColor Red
                }
            }
        } else {
            Write-Host "  ℹ No test products found" -ForegroundColor Yellow
        }
        
        Write-Host ""
        Write-Host "[Step 3] Searching for test groups by name..." -ForegroundColor Yellow
        
        $testGroupNames = @("Main Dishes", "Salads", "Pizzas", "Healthy Options")
        $foundGroups = @()
        
        foreach ($groupName in $testGroupNames) {
            try {
                $response = Invoke-RestMethod -Uri "$baseUrl/groups?filter[name]=$groupName" -Method GET -Headers $headers
                if ($response.data -and $response.data.Count -gt 0) {
                    $foundGroups += $response.data[0]
                    Write-Host "  ✓ Found group: $($response.data[0].name) (ID: $($response.data[0].id))" -ForegroundColor Green
                }
            } catch {
                Write-Host "  ✗ Error searching for group $groupName : $($_.Exception.Message)" -ForegroundColor Red
            }
        }
        
        if ($foundGroups.Count -gt 0) {
            Write-Host ""
            Write-Host "[Step 4] Deleting found groups..." -ForegroundColor Yellow
            
            foreach ($group in $foundGroups) {
                try {
                    Invoke-RestMethod -Uri "$baseUrl/groups/$($group.id)" -Method DELETE -Headers $headers
                    Write-Host "  ✓ Deleted group: $($group.name) (ID: $($group.id))" -ForegroundColor Green
                } catch {
                    Write-Host "  ✗ Error deleting group $($group.id) : $($_.Exception.Message)" -ForegroundColor Red
                }
            }
        } else {
            Write-Host "  ℹ No test groups found" -ForegroundColor Yellow
        }
        
        Write-Host ""
        Write-Host "✓ Manual cleanup completed!" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Cleanup Summary" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "All test data has been removed from Foodics." -ForegroundColor Green
Write-Host "Please verify in Foodics dashboard that:" -ForegroundColor Yellow
Write-Host "  1. Test products are deleted" -ForegroundColor White
Write-Host "  2. Test groups are deleted" -ForegroundColor White
Write-Host "  3. Integration data is cleaned up on Talabat side" -ForegroundColor White
Write-Host ""

