# Loyalty and Stored Value Domain Patterns

Reference guide for implementing loyalty programs and stored value card management in Simphony Extension Applications.

**Source:** SovinoLoyalty project analysis (115 files, 4 scripts, medium complexity)

---

## 1. Background Service Pattern (IService)

### Problem
Loyalty systems need continuous background operations:
- Periodic member synchronization from external API
- Real-time status monitoring
- Balance updates and cache refreshing
- Health checks and connection monitoring

These operations must run independently of POS user interactions.

### Solution: IService Interface with Continuous Background Processing

**IService interface:**
```csharp
public interface IService
{
    /// <summary>
    /// Service name for logging and identification
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Called once when service starts
    /// </summary>
    void Start();

    /// <summary>
    /// Called continuously in background thread
    /// Returns true to continue running, false to stop
    /// </summary>
    bool Execute();

    /// <summary>
    /// Called once when service stops
    /// </summary>
    void Stop();
}
```

**Member sync service implementation:**
```csharp
public class MemberSyncService : IService
{
    private readonly ILogManager _logger;
    private readonly ILoyaltyClient _loyaltyClient;
    private readonly Config _config;
    private DateTime _lastSync;

    public MemberSyncService(
        ILogManager logger,
        ILoyaltyClient loyaltyClient,
        IConfigurationClient configClient)
    {
        _logger = logger;
        _loyaltyClient = loyaltyClient;
        _config = configClient.ReadConfig();
        _lastSync = DateTime.MinValue;
    }

    public string Name => nameof(MemberSyncService);

    public void Start()
    {
        _logger.LogInfo($"{Name} starting...");
        _lastSync = DateTime.MinValue;  // Force initial sync
    }

    public bool Execute()
    {
        try
        {
            // Check if sync interval has elapsed
            var timeSinceLastSync = DateTime.Now - _lastSync;
            if (timeSinceLastSync.TotalMinutes < _config.MemberSyncIntervalMinutes)
            {
                Thread.Sleep(5000);  // Sleep 5 seconds before checking again
                return true;  // Continue running
            }

            // Perform member sync
            _logger.LogInfo($"{Name} performing member sync");
            var members = _loyaltyClient.GetAllMembers();

            foreach (var member in members)
            {
                // Update local cache or database
                _databaseClient.UpsertMember(member);
            }

            _lastSync = DateTime.Now;
            _logger.LogInfo($"{Name} sync completed, {members.Count} members updated");

            return true;  // Continue running
        }
        catch (Exception e)
        {
            _logger.LogException($"{Name} error during sync", e);
            Thread.Sleep(30000);  // Wait 30 seconds before retry
            return true;  // Continue running despite error
        }
    }

    public void Stop()
    {
        _logger.LogInfo($"{Name} stopping...");
    }
}
```

**Service host in ExtensionApplicationService:**
```csharp
public class SimphonyExtensionApplicationService : ExtensionApplicationService
{
    private readonly List<IService> _services;
    private readonly List<Thread> _serviceThreads;
    private bool _running;

    public SimphonyExtensionApplicationService()
    {
        DependencyManager.Install<SimphonyDependencies>();

        _services = DependencyManager.ResolveAll<IService>().ToList();
        _serviceThreads = new List<Thread>();
        _running = false;
    }

    public override void Start()
    {
        _running = true;

        foreach (var service in _services)
        {
            service.Start();

            var thread = new Thread(() =>
            {
                while (_running && service.Execute())
                {
                    // Service.Execute() controls the loop
                }
            })
            {
                IsBackground = true,
                Name = service.Name
            };

            thread.Start();
            _serviceThreads.Add(thread);
        }
    }

    public override void Stop()
    {
        _running = false;

        foreach (var service in _services)
        {
            service.Stop();
        }

        foreach (var thread in _serviceThreads)
        {
            thread.Join(TimeSpan.FromSeconds(5));  // Wait max 5 seconds
        }

        _serviceThreads.Clear();
    }
}
```

**Dependency registration:**
```csharp
// In SimphonyDependencies.Install():
DependencyManager.RegisterByInstance<IService>(
    DependencyManager.Resolve<MemberSyncService>()
);
```

**Benefits:**
- Continuous background processing without user interaction
- Error isolation - service errors don't crash POS
- Clean start/stop lifecycle
- Multiple services can run independently

**When to use:**
- Periodic data synchronization
- Cache refreshing
- Health monitoring
- Status polling
- Real-time updates from external systems

**Important notes:**
- Always use `Thread.Sleep()` in Execute() to avoid CPU spinning
- Return `true` from Execute() to continue, `false` to stop
- Handle exceptions gracefully - don't let them stop the service
- Use background threads (`IsBackground = true`)

---

## 2. Event Handler Pattern (IEventHandler)

### Problem
Loyalty operations need to respond to specific POS events:
- Service charge events (applying loyalty discounts)
- Tender media events (redeeming stored value cards)
- Item sold events (accumulating points)
- Check operations (member identification, balance lookup)

Event handling logic should be separated from scripts for:
- Single Responsibility Principle
- Easier testing
- Event-specific configuration
- Reusable event handlers across multiple events

### Solution: IEventHandler Interface with Typed Event Arguments

**IEventHandler interface:**
```csharp
public interface IEventHandler
{
    /// <summary>
    /// Event handler name for logging and registration
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Process an event and return result
    /// </summary>
    /// <param name="eventArgs">Typed event arguments</param>
    /// <returns>Result indicating success/failure and any data</returns>
    EventProcessingResult ProcessEvent(AbstractEventArgs eventArgs);
}

public class EventProcessingResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public object Data { get; set; }
}

public abstract class AbstractEventArgs
{
    public string EventName { get; set; }
    public DateTime Timestamp { get; set; }
}
```

**Service charge event handler (loyalty discount application):**
```csharp
public class LoyaltyDiscountEventHandler : IEventHandler
{
    private readonly ILogManager _logger;
    private readonly ILoyaltyClient _loyaltyClient;
    private readonly IOpsContextClient _opsContext;
    private readonly Config _config;

    public LoyaltyDiscountEventHandler(
        ILogManager logger,
        ILoyaltyClient loyaltyClient,
        IOpsContextClient opsContext,
        IConfigurationClient configClient)
    {
        _logger = logger;
        _loyaltyClient = loyaltyClient;
        _opsContext = opsContext;
        _config = configClient.ReadConfig();
    }

    public string Name => nameof(LoyaltyDiscountEventHandler);

    public EventProcessingResult ProcessEvent(AbstractEventArgs eventArgs)
    {
        if (!(eventArgs is OpsServiceChargePreviewArgs serviceChargeArgs))
        {
            return new EventProcessingResult
            {
                Success = false,
                ErrorMessage = "Invalid event args type"
            };
        }

        try
        {
            // Get member ID from check (custom data or alternate ID)
            var memberId = _opsContext.GetCheckAlternateId();
            if (string.IsNullOrEmpty(memberId))
            {
                _logger.LogDebug("No member ID on check, skipping loyalty discount");
                return new EventProcessingResult { Success = true };
            }

            // Look up member from loyalty API
            var member = _loyaltyClient.GetMemberById(memberId);
            if (member == null)
            {
                _logger.LogWarn($"Member {memberId} not found in loyalty system");
                return new EventProcessingResult { Success = true };
            }

            // Calculate discount based on member tier
            var discountPercent = GetDiscountForTier(member.TierLevel);
            if (discountPercent <= 0)
            {
                _logger.LogDebug($"No discount for tier {member.TierLevel}");
                return new EventProcessingResult { Success = true };
            }

            // Get check total
            var checkInfo = _opsContext.GetCheckInfo();
            var checkTotal = checkInfo.CheckTotal;
            var discountAmount = checkTotal * (discountPercent / 100m);

            // Apply service charge as discount (negative amount)
            _opsContext.PostCheckDetail(
                CheckDetailType.ServiceCharge,
                _config.LoyaltyDiscountServiceChargeId,
                -discountAmount,
                $"Loyalty Discount {discountPercent}%"
            );

            _logger.LogInfo($"Applied {discountPercent}% loyalty discount (${discountAmount:F2}) for member {memberId}");

            return new EventProcessingResult
            {
                Success = true,
                Data = new { DiscountPercent = discountPercent, DiscountAmount = discountAmount }
            };
        }
        catch (Exception e)
        {
            _logger.LogException("Error processing loyalty discount", e);
            return new EventProcessingResult
            {
                Success = false,
                ErrorMessage = ExceptionHelper.GetFirstException(e).Message
            };
        }
    }

    private decimal GetDiscountForTier(string tierLevel)
    {
        var tierConfig = _config.TierConfigs?.FirstOrDefault(x => x.TierLevel == tierLevel);
        return tierConfig?.DiscountPercent ?? 0;
    }
}
```

**Typed event arguments wrapper:**
```csharp
public class OpsServiceChargePreviewArgs : AbstractEventArgs
{
    private readonly object _originalArgs;

    public OpsServiceChargePreviewArgs(object args)
    {
        _originalArgs = args;
        EventName = "ServiceChargePreviewEvent";
        Timestamp = DateTime.Now;
    }

    // Wrap Simphony event args properties
    public int ServiceChargeId
    {
        get
        {
            var prop = _originalArgs.GetType().GetProperty("ServiceChargeId");
            return (int)prop?.GetValue(_originalArgs);
        }
    }

    // Additional typed properties as needed...
}
```

**Event registration with handler:**
```csharp
// In SimphonyExtensibilityApplication constructor:
public SimphonyExtensibilityApplication(IExecutionContext context) : base(context)
{
    // ... initialization

    // Register ServiceChargePreviewEvent with handler
    this.ServiceChargePreviewEvent += (sender, args) =>
    {
        var handler = DependencyManager.Resolve<IEventHandler>(nameof(LoyaltyDiscountEventHandler));
        var eventArgs = new OpsServiceChargePreviewArgs(args);
        var result = handler.ProcessEvent(eventArgs);

        if (!result.Success)
        {
            _logger.LogError($"Event handler failed: {result.ErrorMessage}");
        }
    };
}
```

**Dependency registration:**
```csharp
// In SimphonyDependencies.Install():
DependencyManager.RegisterByType<IEventHandler, LoyaltyDiscountEventHandler>(
    nameof(LoyaltyDiscountEventHandler)
);
```

**Benefits:**
- Separation of event handling from scripts
- Testable event processing logic
- Type-safe event argument wrappers
- Reusable handlers across multiple events
- Consistent error handling and logging

**When to use:**
- Complex event processing logic
- Multiple events with similar handling
- Event-driven loyalty operations
- Need for isolated event testing
- Event-specific configuration

---

## 3. Loyalty API Integration Pattern

### Problem
External loyalty systems require:
- HTTP/REST API communication
- Authentication (API keys, OAuth)
- Request/response serialization
- Error handling and retries
- Timeout management
- Connection health monitoring

### Solution: Dedicated Loyalty Client with Resilient Communication

**ILoyaltyClient interface:**
```csharp
public interface ILoyaltyClient
{
    // Member operations
    Member GetMemberById(string memberId);
    Member GetMemberByCardNumber(string cardNumber);
    Member[] GetAllMembers();
    Member CreateMember(MemberRegistration registration);
    void UpdateMember(Member member);

    // Points operations
    PointsBalance GetPointsBalance(string memberId);
    PointsTransaction AddPoints(string memberId, decimal amount, string description);
    PointsTransaction RedeemPoints(string memberId, int points, string description);

    // Stored value operations
    StoredValueBalance GetStoredValueBalance(string cardNumber);
    StoredValueTransaction AddValue(string cardNumber, decimal amount, string description);
    StoredValueTransaction RedeemValue(string cardNumber, decimal amount, string description);

    // Health check
    bool IsConnected();
}
```

**Loyalty client implementation:**
```csharp
public class LoyaltyApiClient : ILoyaltyClient
{
    private readonly ILogManager _logger;
    private readonly ISerializer _serializer;
    private readonly Config _config;
    private readonly HttpClient _httpClient;

    public LoyaltyApiClient(
        ILogManager logger,
        IConfigurationClient configClient)
    {
        _logger = logger;
        _serializer = DependencyManager.Resolve<ISerializer>("json");
        _config = configClient.ReadConfig();

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_config.LoyaltyApiUrl),
            Timeout = TimeSpan.FromSeconds(_config.LoyaltyApiTimeoutSeconds ?? 30)
        };

        // Add authentication header
        if (!string.IsNullOrEmpty(_config.LoyaltyApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _config.LoyaltyApiKey);
        }
    }

    public Member GetMemberById(string memberId)
    {
        try
        {
            _logger.LogDebug($"Fetching member {memberId}");

            var response = _httpClient.GetAsync($"/api/members/{memberId}").Result;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Loyalty API error: {response.StatusCode}");
                return null;
            }

            var json = response.Content.ReadAsStringAsync().Result;
            var member = _serializer.Deserialize<Member>(json);

            _logger.LogDebug($"Member {memberId} found: {member.FirstName} {member.LastName}");

            return member;
        }
        catch (Exception e)
        {
            _logger.LogException($"Error fetching member {memberId}", e);
            return null;
        }
    }

    public PointsTransaction AddPoints(string memberId, decimal amount, string description)
    {
        try
        {
            var request = new PointsRequest
            {
                MemberId = memberId,
                Amount = amount,
                Description = description,
                Timestamp = DateTime.UtcNow
            };

            var json = _serializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = _httpClient.PostAsync("/api/points/add", content).Result;

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = response.Content.ReadAsStringAsync().Result;
                throw new Exception($"API error {response.StatusCode}: {errorBody}");
            }

            var responseJson = response.Content.ReadAsStringAsync().Result;
            var transaction = _serializer.Deserialize<PointsTransaction>(responseJson);

            _logger.LogInfo($"Added {amount} points to member {memberId}, new balance: {transaction.NewBalance}");

            return transaction;
        }
        catch (Exception e)
        {
            _logger.LogException($"Error adding points for member {memberId}", e);
            throw;
        }
    }

    public bool IsConnected()
    {
        try
        {
            var response = _httpClient.GetAsync("/api/health").Result;
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
```

**Configuration:**
```csharp
public class Config
{
    public string LoyaltyApiUrl { get; set; }
    public string LoyaltyApiKey { get; set; }
    public int? LoyaltyApiTimeoutSeconds { get; set; }

    // Retry configuration
    public int LoyaltyApiRetryCount { get; set; }
    public int LoyaltyApiRetryDelayMs { get; set; }
}
```

**With retry logic:**
```csharp
public class ResilientLoyaltyClient : ILoyaltyClient
{
    private readonly ILoyaltyClient _innerClient;
    private readonly ILogManager _logger;
    private readonly Config _config;

    public ResilientLoyaltyClient(
        ILoyaltyClient innerClient,
        ILogManager logger,
        IConfigurationClient configClient)
    {
        _innerClient = innerClient;
        _logger = logger;
        _config = configClient.ReadConfig();
    }

    public Member GetMemberById(string memberId)
    {
        return ExecuteWithRetry(() => _innerClient.GetMemberById(memberId), nameof(GetMemberById));
    }

    private T ExecuteWithRetry<T>(Func<T> operation, string operationName)
    {
        var retryCount = _config.LoyaltyApiRetryCount;
        var retryDelay = _config.LoyaltyApiRetryDelayMs;

        for (int attempt = 0; attempt <= retryCount; attempt++)
        {
            try
            {
                return operation();
            }
            catch (Exception e)
            {
                if (attempt == retryCount)
                {
                    _logger.LogException($"{operationName} failed after {retryCount} retries", e);
                    throw;
                }

                _logger.LogWarn($"{operationName} failed (attempt {attempt + 1}/{retryCount + 1}), retrying in {retryDelay}ms");
                Thread.Sleep(retryDelay);
            }
        }

        throw new Exception("Retry logic error");
    }
}
```

**Benefits:**
- Centralized API communication
- Consistent error handling
- Configurable timeouts and retries
- Authentication management
- Health monitoring
- Testable with mock implementations

**When to use:**
- External loyalty system integration
- RESTful API communication
- Third-party service integration
- Cloud-based loyalty platforms

---

## 4. Stored Value Card Management Pattern

### Problem
Stored value cards (gift cards, prepaid cards) require:
- Balance lookup before tender
- Real-time balance validation
- Transaction posting (add value, redeem value)
- Offline mode handling (when API unavailable)
- Multi-tender scenarios (partial redemption)
- Receipt printing with balance information

### Solution: Stored Value Client with Balance Caching and Offline Support

**Stored value operations:**
```csharp
public class StoredValueScript : IScript
{
    private readonly ILoyaltyClient _loyaltyClient;
    private readonly IOpsContextClient _opsContext;
    private readonly ILogManager _logger;
    private readonly Config _config;

    public void Execute(string functionName, string argument)
    {
        switch (functionName)
        {
            case "GetBalance":
                GetBalance(argument);
                break;
            case "Redeem":
                Redeem(argument);
                break;
            case "AddValue":
                AddValue(argument);
                break;
            default:
                throw new Exception($"Unknown function: {functionName}");
        }
    }

    private void GetBalance(string cardNumber)
    {
        try
        {
            var balance = _loyaltyClient.GetStoredValueBalance(cardNumber);

            if (balance == null)
            {
                _opsContext.ShowError($"Card {cardNumber} not found");
                return;
            }

            _opsContext.ShowMessage(
                $"Card: {cardNumber}\n" +
                $"Balance: ${balance.Amount:F2}\n" +
                $"Status: {balance.Status}"
            );
        }
        catch (Exception e)
        {
            _logger.LogException("Error getting stored value balance", e);
            _opsContext.ShowError("Unable to retrieve card balance");
        }
    }

    private void Redeem(string cardNumber)
    {
        try
        {
            // Get check total
            var checkInfo = _opsContext.GetCheckInfo();
            var checkTotal = checkInfo.CheckTotal;

            if (checkTotal <= 0)
            {
                _opsContext.ShowError("No balance due on check");
                return;
            }

            // Get card balance
            var balance = _loyaltyClient.GetStoredValueBalance(cardNumber);
            if (balance == null)
            {
                _opsContext.ShowError($"Card {cardNumber} not found");
                return;
            }

            if (balance.Amount <= 0)
            {
                _opsContext.ShowError("Card has zero balance");
                return;
            }

            // Calculate redemption amount (lesser of check total or card balance)
            var redemptionAmount = Math.Min(checkTotal, balance.Amount);

            // Prompt user to confirm
            var message = $"Card Balance: ${balance.Amount:F2}\n" +
                         $"Check Total: ${checkTotal:F2}\n" +
                         $"Redeem: ${redemptionAmount:F2}\n\n" +
                         $"Continue?";

            if (!_opsContext.ShowConfirmation(message))
            {
                return;
            }

            // Post tender media to check
            _opsContext.PostCheckDetail(
                CheckDetailType.TenderMedia,
                _config.StoredValueTenderMediaId,
                redemptionAmount,
                $"Gift Card {cardNumber}"
            );

            // Redeem from loyalty API
            var transaction = _loyaltyClient.RedeemValue(cardNumber, redemptionAmount, $"POS Redemption - Check {checkInfo.CheckNumber}");

            _logger.LogInfo($"Redeemed ${redemptionAmount:F2} from card {cardNumber}, new balance: ${transaction.NewBalance:F2}");

            // Show success message with new balance
            _opsContext.ShowMessage(
                $"Redeemed: ${redemptionAmount:F2}\n" +
                $"New Balance: ${transaction.NewBalance:F2}"
            );
        }
        catch (Exception e)
        {
            _logger.LogException("Error redeeming stored value", e);
            _opsContext.ShowError("Unable to redeem card. Please try again.");
        }
    }

    private void AddValue(string argument)
    {
        try
        {
            // Parse argument: "CARDNUMBER:AMOUNT"
            var parts = argument.Split(':');
            if (parts.Length != 2)
            {
                _opsContext.ShowError("Invalid format. Use CARDNUMBER:AMOUNT");
                return;
            }

            var cardNumber = parts[0];
            if (!decimal.TryParse(parts[1], out decimal amount))
            {
                _opsContext.ShowError("Invalid amount");
                return;
            }

            // Get current balance
            var balance = _loyaltyClient.GetStoredValueBalance(cardNumber);
            var currentBalance = balance?.Amount ?? 0;

            // Confirm with user
            var message = $"Add ${amount:F2} to card {cardNumber}?\n" +
                         $"Current Balance: ${currentBalance:F2}\n" +
                         $"New Balance: ${currentBalance + amount:F2}";

            if (!_opsContext.ShowConfirmation(message))
            {
                return;
            }

            // Add value via loyalty API
            var transaction = _loyaltyClient.AddValue(cardNumber, amount, "POS Value Add");

            _logger.LogInfo($"Added ${amount:F2} to card {cardNumber}, new balance: ${transaction.NewBalance:F2}");

            // Show success
            _opsContext.ShowMessage($"Added ${amount:F2}\nNew Balance: ${transaction.NewBalance:F2}");

            // Post to check if needed (for sale of gift card)
            if (_config.PostGiftCardSaleToCheck)
            {
                _opsContext.PostCheckDetail(
                    CheckDetailType.MenuItem,
                    _config.GiftCardSaleMenuItemId,
                    amount,
                    $"Gift Card Sale {cardNumber}"
                );
            }
        }
        catch (Exception e)
        {
            _logger.LogException("Error adding stored value", e);
            _opsContext.ShowError("Unable to add value to card");
        }
    }
}
```

**Offline mode handling with local cache:**
```csharp
public class CachedLoyaltyClient : ILoyaltyClient
{
    private readonly ILoyaltyClient _apiClient;
    private readonly IDatabaseClient _cacheDb;
    private readonly ILogManager _logger;

    public StoredValueBalance GetStoredValueBalance(string cardNumber)
    {
        try
        {
            // Try API first
            var balance = _apiClient.GetStoredValueBalance(cardNumber);

            // Cache result
            if (balance != null)
            {
                _cacheDb.UpsertStoredValueBalance(balance);
            }

            return balance;
        }
        catch (Exception e)
        {
            _logger.LogWarn($"API unavailable, using cached balance for {cardNumber}");

            // Fall back to cache
            return _cacheDb.GetStoredValueBalance(cardNumber);
        }
    }

    public StoredValueTransaction RedeemValue(string cardNumber, decimal amount, string description)
    {
        // Always require API for transactions (don't allow offline redemption)
        return _apiClient.RedeemValue(cardNumber, amount, description);
    }
}
```

**Benefits:**
- Real-time balance validation
- Partial redemption support
- Offline balance lookup (cached)
- User confirmation before redemption
- Audit trail of all transactions

**When to use:**
- Gift card programs
- Prepaid card systems
- Store credit management
- Loyalty point redemption as currency

---

## 5. Customer/Member Management Pattern

### Problem
Loyalty programs need customer/member management:
- Member registration at POS
- Member lookup (by ID, phone, email, card number)
- Profile updates (address, phone, email)
- Tier management (automatic upgrades)
- Member search and selection
- Linking members to checks

### Solution: Member Management Service with Multiple Lookup Methods

**Member registration script:**
```csharp
public class MemberRegistration : IScript
{
    private readonly ILoyaltyClient _loyaltyClient;
    private readonly IOpsContextClient _opsContext;
    private readonly ILogManager _logger;

    public void Execute(string functionName, string argument)
    {
        try
        {
            // Collect member information from user
            var firstName = _opsContext.GetTextInput("First Name:");
            if (string.IsNullOrEmpty(firstName)) return;

            var lastName = _opsContext.GetTextInput("Last Name:");
            if (string.IsNullOrEmpty(lastName)) return;

            var email = _opsContext.GetTextInput("Email:");
            var phone = _opsContext.GetTextInput("Phone:");

            // Validate email format
            if (!string.IsNullOrEmpty(email) && !IsValidEmail(email))
            {
                _opsContext.ShowError("Invalid email format");
                return;
            }

            // Create member registration request
            var registration = new MemberRegistration
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Phone = phone,
                EnrollmentDate = DateTime.Now,
                EnrollmentLocationId = _opsContext.GetPropertyNumber().ToString()
            };

            // Confirm with user
            var confirmMessage = $"Register new member?\n\n" +
                               $"{firstName} {lastName}\n" +
                               $"Email: {email}\n" +
                               $"Phone: {phone}";

            if (!_opsContext.ShowConfirmation(confirmMessage))
            {
                return;
            }

            // Register member via loyalty API
            var member = _loyaltyClient.CreateMember(registration);

            _logger.LogInfo($"Registered new member: {member.MemberId} - {member.FirstName} {member.LastName}");

            // Show member ID
            _opsContext.ShowMessage(
                $"Member Registered!\n\n" +
                $"Member ID: {member.MemberId}\n" +
                $"Name: {member.FirstName} {member.LastName}\n" +
                $"Tier: {member.TierLevel}"
            );

            // Optionally link to current check
            if (_opsContext.GetCheckInfo().CheckNumber > 0)
            {
                if (_opsContext.ShowConfirmation("Link member to current check?"))
                {
                    _opsContext.SetAlternativeId(member.MemberId);
                    _logger.LogInfo($"Linked member {member.MemberId} to check {_opsContext.GetCheckInfo().CheckNumber}");
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogException("Error registering member", e);
            _opsContext.ShowError("Unable to register member");
        }
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
```

**Member lookup script with multiple search methods:**
```csharp
public class MemberLookup : IScript
{
    private readonly ILoyaltyClient _loyaltyClient;
    private readonly IOpsContextClient _opsContext;
    private readonly ILogManager _logger;

    public void Execute(string functionName, string argument)
    {
        try
        {
            Member member = null;

            // Determine lookup method based on function name
            switch (functionName)
            {
                case "ById":
                    member = LookupById();
                    break;
                case "ByCard":
                    member = LookupByCard();
                    break;
                case "ByPhone":
                    member = LookupByPhone();
                    break;
                case "ByEmail":
                    member = LookupByEmail();
                    break;
                default:
                    // Show menu to user
                    member = LookupWithMenu();
                    break;
            }

            if (member == null)
            {
                _opsContext.ShowError("Member not found");
                return;
            }

            // Display member information
            ShowMemberInfo(member);

            // Optionally link to check
            if (_opsContext.GetCheckInfo().CheckNumber > 0)
            {
                if (_opsContext.ShowConfirmation("Link member to current check?"))
                {
                    _opsContext.SetAlternativeId(member.MemberId);
                    _logger.LogInfo($"Linked member {member.MemberId} to check");
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogException("Error looking up member", e);
            _opsContext.ShowError("Unable to look up member");
        }
    }

    private Member LookupById()
    {
        var memberId = _opsContext.GetTextInput("Enter Member ID:");
        if (string.IsNullOrEmpty(memberId)) return null;

        return _loyaltyClient.GetMemberById(memberId);
    }

    private Member LookupByCard()
    {
        var cardNumber = _opsContext.GetTextInput("Scan or enter card number:");
        if (string.IsNullOrEmpty(cardNumber)) return null;

        return _loyaltyClient.GetMemberByCardNumber(cardNumber);
    }

    private Member LookupByPhone()
    {
        var phone = _opsContext.GetTextInput("Enter phone number:");
        if (string.IsNullOrEmpty(phone)) return null;

        return _loyaltyClient.GetMemberByPhone(phone);
    }

    private void ShowMemberInfo(Member member)
    {
        var pointsBalance = _loyaltyClient.GetPointsBalance(member.MemberId);

        var message = $"Member: {member.FirstName} {member.LastName}\n" +
                     $"ID: {member.MemberId}\n" +
                     $"Tier: {member.TierLevel}\n" +
                     $"Points: {pointsBalance?.Points ?? 0}\n" +
                     $"Email: {member.Email}\n" +
                     $"Phone: {member.Phone}";

        _opsContext.ShowMessage(message);
    }
}
```

**Benefits:**
- Multiple lookup methods (ID, card, phone, email)
- User-friendly POS registration
- Member information display
- Check linking capability
- Input validation

**When to use:**
- Member-based loyalty programs
- Customer relationship management
- Personalized service
- Targeted promotions

---

## Summary

### When to Use These Patterns

**Use loyalty/stored value patterns when building:**
- Loyalty point programs
- Gift card/stored value systems
- Member rewards and discounts
- Customer relationship management
- Tiered membership programs

### Pattern Combination Recommendations

**Basic Loyalty Implementation:**
1. ILoyaltyClient for API communication
2. Member lookup and registration scripts
3. Points/balance tracking

**Advanced Loyalty Implementation:**
Add background services for sync, event handlers for automatic discount application, and offline caching for resilience.

### Integration with Universal Patterns

These domain patterns build on universal Simphony Extension Application patterns:
- Use **Interface-First Design** for ILoyaltyClient, IService, IEventHandler
- Use **Custom DI Container** for service and handler registration
- Use **Logging Framework** for API call tracking and error logging
- Use **Configuration Management** for API URLs, timeouts, retry settings
- Use **IScript interface** for user-facing loyalty operations

### Key Takeaways

1. **Background services enable continuous sync** - IService pattern for periodic operations
2. **Event handlers separate concerns** - IEventHandler pattern for POS event responses
3. **API resilience is critical** - Retry logic, timeouts, offline fallback
4. **Real-time balance validation** - Always check balance before redemption
5. **Audit trail everything** - Log all loyalty transactions for compliance
6. **Multiple lookup methods** - Support ID, card, phone, email searches
