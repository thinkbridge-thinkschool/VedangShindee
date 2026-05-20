# Piece 5 — JWT Auth Output

## Login Endpoint Code

```csharp
// Endpoints/AuthEndpoints.cs
app.MapPost("/api/auth/login", async (LoginRequest request, AppDbContext db, IConfiguration config) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
    if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        return Results.Unauthorized();

    var jwtKey = config["Jwt:Key"]!;
    var expiresInMinutes = config.GetValue<int>("Jwt:ExpiresInMinutes", 60);
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: config["Jwt:Issuer"],
        audience: config["Jwt:Audience"],
        claims:
        [
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email)
        ],
        expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
        signingCredentials: creds);

    var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
    var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    return Results.Ok(new LoginResponse(accessToken, refreshToken, expiresInMinutes * 60));
});
```

---

## Three Curls

### 1. POST without token → 401

$r = Invoke-WebRequest -Method POST http://localhost:5051/api/auth/login -ContentType "application/json" -Body '{"email":"test@example.com","p
password":"password123"}'

**Response** -
{"access_token":"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwiZW1haWwiOiJ0ZXN0QGV4YW1wbGUuY29tIiwiZXhwIjoxNzc5MjgwNjAxLCJpc3MiOiJRdW90ZXNBcGkiLCJhdWQiOiJRdW90ZXNBcGkifQ.mDrFAQ_N1lt9jth2sVr4kojdk5GuTXsfZPLu8VeiEGs","refresh_token":"ivYP9PV7bDUaJMS0a5hRxkqKAf6E9rF8/vJwkK3LQuI=","expires_in":3600}
```

---

### 2. Login, then POST with valid token → 201

201 - Valid Token
 $token = ($r.Content | ConvertFrom-Json).access_token
>> Invoke-WebRequest -Method POST http://localhost:5051/api/quotes -ContentType "application/json" -Headers @{ Authorization = "Bearer $token" } -Body '{"author":"Aristotle","text":"The whole is 
greater than the sum of its parts"}' | Select-Object StatusCode, @{n="Body";e={$_.Content}}
>>
    greater than the sum of its parts"}' | Select-Object StatusCode, @{n="Body"\x3be={$_.Content}}\x0a;510416e8-b248-4be6-8f4b-429630703b79

**Response** -

       201 {"id":3,"author":"Aristotle","text":"The whole is greater than the sum of its parts","createdAt":"2026-05-20T11:37:03.5002554+00:00"}


### 3. POST with expired token → 401 + WWW-Authenticate error

try {
>>     Invoke-WebRequest -Method POST http://localhost:5051/api/quotes `
>>       -ContentType "application/json" `
>>       -Headers @{ Authorization = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwiZXhwIjoxNjAwMDAwMDAwfQ.invalid" } `
>>       -Body '{"author":"Test","text":"Expired"}'
>> } catch {
>>     "Status: $($_.Exception.Response.StatusCode.Value__)"
>>     "WWW-Authenticate: $($_.Exception.Response.Headers['WWW-Authenticate'])"
>>       -ContentType "application/json" `
>>       -Headers @{ Authorization = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwiZXhwIjoxNjAwMDAwMDAwfQ.invalid" } `
>>       -Body '{"author":"Test","text":"Expired"}'
>>       -ContentType "application/json" `
>>       -Headers @{ Authorization = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwiZXhwIjoxNjAwMDAwMDAwfQ.invalid" } `
>>       -ContentType "application/json" `
>>       -ContentType "application/json" `
>>       -ContentType "application/json" `
>>       -Headers @{ Authorization = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwiZXhwIjoxNjAwMDAwMDAwfQ.invalid" } `
>>       -ContentType "application/json" `
>>       -Headers @{ Authorization = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwiZXhwIjoxNjAwMDAwMDAwfQ.invalid" } `
>>       -ContentType "application/json" `
>>       -Headers @{ Authorization = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwiZXhwIjoxNjAwMDAwMDAwfQ.invalid" } `
>>       -Body '{"author":"Test","text":"Expired"}'
>>       -ContentType "application/json" `
>>       -Headers @{ Authorization = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwiZXhwIjoxNjAwMDAwMDAwfQ.invalid" } `
>>       -ContentType "application/json" `
>>       -Headers @{ Authorization = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwiZXhwIjoxNjAwMDAwMDAwfQ.invalid" } `
>>       -Body '{"author":"Test","text":"Expired"}'
>> } catch {
>>       -Headers @{ Authorization = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwiZXhwIjoxNjAwMDAwMDAwfQ.invalid" } `
>>       -Body '{"author":"Test","text":"Expired"}'
>>       -Headers @{ Authorization = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwiZXhwIjoxNjAwMDAwMDAwfQ.invalid" } `
>>       -Headers @{ Authorization = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwiZXhwIjoxNjAwMDAwMDAwfQ.invalid" } `
>>       -Body '{"author":"Test","text":"Expired"}'
>> } catch {
>>     "Status: $($_.Exception.Response.StatusCode.Value__)"
>>     "WWW-Authenticate: $($_.Exception.Response.Headers['WWW-Authenticate'])"
>> }

**Response** -
Status: 401
WWW-Authenticate: Bearer error="invalid_token", error_description="The signature key was not found"






