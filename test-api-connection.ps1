# Foodics API Connection Test Script
# This script tests your Foodics API connection and token validity

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Foodics API Connection Diagnostic Tool" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Read token from collection or ask user
$token = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsImp0aSI6IjQ2NjFkMTFhZDdhYzE4ZWQ0MGM5OTIxMjEzYzAzZDJjNWJiYmMwNjMzODY4ZmM2OGM5OTZhMDE3NjdiNWM1NGI0MDNjNzYyZmQwOWM1NGExIn0.eyJhdWQiOiI5NzQ1M2M3Mi03ZmI4LTQ5YmYtYmUxOC1hYjAwMWI4MWY3NzMiLCJqdGkiOiI0NjYxZDExYWQ3YWMxOGVkNDBjOTkyMTIxM2MwM2QyYzViYmJjMDYzMzg2OGZjNjhjOTk2YTAxNzY3YjVjNTRiNDAzYzc2MmZkMDljNTRhMSIsImlhdCI6MTY4Nzc1NjkzNCwibmJmIjoxNjg3NzU2OTM0LCJleHAiOjE4NDU2MDk3MzQsInN1YiI6Ijk3YmRmOWFlLTA1OWEtNGUzZC04ODIxLWM4MzgxZTQ1MmNiOSIsInNjb3BlcyI6W10sImJ1c2luZXNzIjoiOTdiZGY5YWUtMTRhZC00OTMxLWJiOWQtNWM0ODI0NjQ5NjhjIiwicmVmZXJlbmNlIjoiNjYzMzAwIn0.MRF86GIKzUlaVSWrO95kwwiji_DjML7cHQF0dU6Fh9bF5LW0IqamII_rbVba4GdluPeBrYNf-xxVIrKqyoHhNakBOYX-pT6U9_n7UMlohcy9cQ9OwInAQtQbq9JAdludK3AGd3o4R8T2sMciYhtPQxT3kLCj1VutzQLm7y8p33-_pGaksRWm76ngvkZZ4lFqe8nGHIOCLH74NO8STTIafiTJUyjPRTd2hl31Hwr7F08Vscie9vsbYAYK8QFfuRljaK7Lzx34-jHyIbamounIAqSiC8Z1LFC_r9ZOa3M8YmXiolEwPp2aVmz0z9c9vhcXcFy_56-gB-N_yJhYPtOVd9ev_Q5Ckh5fahrHFSdVaHHhbxpzbuqVJ67rm-Gn_1bPQhhIuOSt41GJAK1qOeEa97sH1qk0M6wK57JffL1UFhr9eJ7oWcz4Qc_jx_BPmrqheOXyc9hRvL9Nk35yYgJhurWyd0hEQmrJWgJcRoUAfHv5Nkgt2OAEG3MCu7vGC3uLe6Ffn7I9Qzb6yQEGj8wxZ1-KaGyhj0Giee-WHg6zedJddLMTGTPfihqrHMKPiG5-qOykPmUp1c8uC45CySFGG6pBkvcFON7rirJ9DRXf-H1xW3eyc1-hq9swSaxYO1ELwFv420QP4ZgPd-Qytli0kx1pb5kmbNnHV1-S0D-ofF4"

Write-Host "Testing with token (first 50 chars): $($token.Substring(0, 50))..." -ForegroundColor Gray
Write-Host ""

# Try different base URLs
$baseUrls = @(
    "https://api-sandbox.foodics.com/v5",
    "https://api.foodics.com/v5",
    "https://api-sandbox.foodics.dev/v5"
)

$endpoints = @(
    "/categories",
    "/products",
    "/groups"
)

foreach ($baseUrl in $baseUrls) {
    Write-Host "Testing base URL: $baseUrl" -ForegroundColor Yellow
    
    $headers = @{
        "Authorization" = "Bearer $token"
        "Accept" = "application/json"
        "Content-Type" = "application/json"
    }
    
    foreach ($endpoint in $endpoints) {
        $fullUrl = "$baseUrl$endpoint"
        try {
            Write-Host "  Testing: $endpoint ... " -NoNewline
            $response = Invoke-RestMethod -Uri $fullUrl -Method GET -Headers $headers -ErrorAction Stop
            Write-Host "[SUCCESS]" -ForegroundColor Green
            Write-Host "    Response data count: $($response.data.Count)" -ForegroundColor Gray
            
            # If successful, save the working configuration
            $workingConfig = @{
                base_url = $baseUrl
                endpoint = $endpoint
                token_valid = $true
                timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
            }
            $workingConfig | ConvertTo-Json | Out-File -FilePath "working-api-config.json" -Encoding UTF8
            
            Write-Host ""
            Write-Host "[SUCCESS] Found working API configuration!" -ForegroundColor Green
            Write-Host "  Base URL: $baseUrl" -ForegroundColor White
            Write-Host "  Token is valid!" -ForegroundColor White
            Write-Host ""
            Write-Host "Configuration saved to: working-api-config.json" -ForegroundColor Cyan
            Write-Host ""
            Write-Host "You can now run: .\create-test-data.ps1" -ForegroundColor Green
            exit 0
            
        } catch {
            $statusCode = $_.Exception.Response.StatusCode.value__
            if ($statusCode) {
                Write-Host "[FAILED] - Status: $statusCode" -ForegroundColor Red
            } else {
                Write-Host "[FAILED] - $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }
    Write-Host ""
}

Write-Host "============================================" -ForegroundColor Red
Write-Host "[ERROR] No working API configuration found" -ForegroundColor Red
Write-Host "============================================" -ForegroundColor Red
Write-Host ""
Write-Host "Possible solutions:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. GET A NEW TOKEN:" -ForegroundColor Cyan
Write-Host "   - Your current token appears to be expired" -ForegroundColor White
Write-Host "   - Log in to Foodics Console: https://console.foodics.com/" -ForegroundColor White
Write-Host "   - Go to Settings > API > Generate New Token" -ForegroundColor White
Write-Host "   - Copy the new token" -ForegroundColor White
Write-Host "   - Update line 13 in this script with new token" -ForegroundColor White
Write-Host ""
Write-Host "2. CHECK POSTMAN COLLECTION:" -ForegroundColor Cyan
Write-Host "   - Open your Postman collection" -ForegroundColor White
Write-Host "   - Check if requests work there" -ForegroundColor White
Write-Host "   - Copy working token from Postman" -ForegroundColor White
Write-Host "   - Collection ID: 12882056-288945c6-2cf6-4f05-bc30-8cef666bdf74" -ForegroundColor White
Write-Host ""
Write-Host "3. USE POSTMAN TO CREATE TEST DATA:" -ForegroundColor Cyan
Write-Host "   - Import the collection into Postman" -ForegroundColor White
Write-Host "   - Manually run: Create Group (x4)" -ForegroundColor White
Write-Host "   - Manually run: Create Product (x3)" -ForegroundColor White
Write-Host ""

