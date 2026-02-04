# List all groups in Foodics

$baseUrl = "https://api-sandbox.foodics.com/v5"
$token = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsImp0aSI6IjQ2NjFkMTFhZDdhYzE4ZWQ0MGM5OTIxMjEzYzAzZDJjNWJiYmMwNjMzODY4ZmM2OGM5OTZhMDE3NjdiNWM1NGI0MDNjNzYyZmQwOWM1NGExIn0.eyJhdWQiOiI5NzQ1M2M3Mi03ZmI4LTQ5YmYtYmUxOC1hYjAwMWI4MWY3NzMiLCJqdGkiOiI0NjYxZDExYWQ3YWMxOGVkNDBjOTkyMTIxM2MwM2QyYzViYmJjMDYzMzg2OGZjNjhjOTk2YTAxNzY3YjVjNTRiNDAzYzc2MmZkMDljNTRhMSIsImlhdCI6MTY4Nzc1NjkzNCwibmJmIjoxNjg3NzU2OTM0LCJleHAiOjE4NDU2MDk3MzQsInN1YiI6Ijk3YmRmOWFlLTA1OWEtNGUzZC04ODIxLWM4MzgxZTQ1MmNiOSIsInNjb3BlcyI6W10sImJ1c2luZXNzIjoiOTdiZGY5YWUtMTRhZC00OTMxLWJiOWQtNWM0ODI0NjQ5NjhjIiwicmVmZXJlbmNlIjoiNjYzMzAwIn0.MRF86GIKzUlaVSWrO95kwwiji_DjML7cHQF0dU6Fh9bF5LW0IqamII_rbVba4GdluPeBrYNf-xxVIrKqyoHhNakBOYX-pT6U9_n7UMlohcy9cQ9OwInAQtQbq9JAdludK3AGd3o4R8T2sMciYhtPQxT3kLCj1VutzQLm7y8p33-_pGaksRWm76ngvkZZ4lFqe8nGHIOCLH74NO8STTIafiTJUyjPRTd2hl31Hwr7F08Vscie9vsbYAYK8QFfuRljaK7Lzx34-jHyIbamounIAqSiC8Z1LFC_r9ZOa3M8YmXiolEwPp2aVmz0z9c9vhcXcFy_56-gB-N_yJhYPtOVd9ev_Q5Ckh5fahrHFSdVaHHhbxpzbuqVJ67rm-Gn_1bPQhhIuOSt41GJAK1qOeEa97sH1qk0M6wK57JffL1UFhr9eJ7oWcz4Qc_jx_BPmrqheOXyc9hRvL9Nk35yYgJhurWyd0hEQmrJWgJcRoUAfHv5Nkgt2OAEG3MCu7vGC3uLe6Ffn7I9Qzb6yQEGj8wxZ1-KaGyhj0Giee-WHg6zedJddLMTGTPfihqrHMKPiG5-qOykPmUp1c8uC45CySFGG6pBkvcFON7rirJ9DRXf-H1xW3eyc1-hq9swSaxYO1ELwFv420QP4ZgPd-Qytli0kx1pb5kmbNnHV1-S0D-ofF4"

$headers = @{
    "Authorization" = "Bearer $token"
    "Accept" = "application/json"
    "Content-Type" = "application/json"
}

Write-Host "Fetching existing groups from Foodics..." -ForegroundColor Yellow
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/groups?filter[is_deleted]=false" -Method GET -Headers $headers
    
    $groupCount = $response.data.Count
    Write-Host "Found $groupCount groups:" -ForegroundColor Green
    Write-Host ""
    
    foreach ($group in $response.data) {
        Write-Host "  - $($group.name)" -ForegroundColor White
        Write-Host "    ID: $($group.id)" -ForegroundColor Gray
        Write-Host ""
    }
    
    if ($groupCount -eq 0) {
        Write-Host "No groups found. Creating groups first..." -ForegroundColor Yellow
        Write-Host "Use the create-test-data.ps1 script to create groups" -ForegroundColor Yellow
    } else {
        Write-Host "Copy the IDs above to use in create-products-in-groups.ps1" -ForegroundColor Cyan
    }
    
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}


