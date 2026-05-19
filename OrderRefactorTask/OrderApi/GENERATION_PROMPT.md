# Generation Prompt for Bad OrderController

## Prompt Used to Generate OrderController.cs

Write me a deliberately-bad OrderController.cs for an ASP.NET Core 10 minimal API with the following requirements:

1. **Size**: Approximately 300 lines
2. **Structure**: One single, giant POST /api/orders action that handles everything
3. **Antipatterns to include**:
   - Mix business logic, EF data access, validation, and HTTP shape all inline
   - Include 4 empty catch {} blocks that swallow exceptions silently
   - Use synchronous EF database calls inside an async action (blocking calls)
   - Return plain `object` types instead of typed responses (no DTO/response classes)
   - No unit tests or integration tests whatsoever
   - Include couple of subtle bugs:
     * Off-by-one error in validation or calculation
     * Potential null reference dereference
     * String parsing without proper validation
4. **Domain**: Order management system with:
   - Order creation with items
   - Customer info processing
   - Inventory checks
   - Order total calculation
   - Database persistence directly in the action

Make it realistic enough that junior developers might write this in production - show common mistakes, not absurd ones. Include TODO comments that suggest "features to add later" that hint at what should be in a proper architecture.

Do not include any explanations - just the raw controller code. Make it look like legacy code that needs serious refactoring.
