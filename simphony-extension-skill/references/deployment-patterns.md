# Deployment Patterns for Simphony Extension Applications

## Overview

Extension deployment in Simphony is managed through the Enterprise Management Console (EMC) with Oracle Cloud hosting. Understanding the deployment lifecycle is critical to avoid common pitfalls and troubleshooting issues.

## Extension Components

An extension typically consists of:

1. **Primary DLL** - The compiled extension assembly
   - **CRITICAL: Must have a DiskFile name in EMC**
   - Without DiskFile name, the extension will NOT be instantiated
   - Contains `IExtensibilityAssemblyFactory` implementation

2. **Configuration XML** (optional but common)
   - Extension configuration stored as Extension Application Content
   - Read via `IConfigurationClient` implementations

3. **Additional Files** (as needed)
   - Dependent DLLs (third-party libraries, shared assemblies)
   - Images (logos, icons, UI resources)
   - Templates (report templates, export formats)
   - Data files (mappings, lookups)

## Deployment Architecture

### Oracle Cloud Hosting

- **EMC Data**: Hosted in Oracle Cloud
- **Extension Content**: Stored in cloud database
- **Distribution**: Automatic sync from cloud to workstations
- **Updates**: Pushed through EMC, downloaded by workstations

### Workstation Deployment

```
Oracle Cloud (EMC)
    ↓
    | Continuous sync (configuration, data)
    | Extension Application Content download
    ↓
Simphony Workstation
    ↓
    | On Simphony start ONLY
    | DLL written to disk
    | Extension instantiated
    ↓
Extension Running
```

## EMC Configuration

### Creating Extension Application

1. **Open EMC** (Enterprise Management Console)
2. **Navigate to**: Configuration → Extension Applications
3. **Create New Extension Application**:
   - Name: Your extension name
   - Description: Extension purpose
   - Active: ✓ (enable extension)

### Adding Extension Content

**Critical Steps:**

1. **Add Primary DLL**:
   - Click "Add Content"
   - Upload your compiled DLL
   - **MANDATORY: Set DiskFile name** (e.g., `YourExtension.dll`)
   - Type: Extension DLL
   - Zone: Assign to appropriate hierarchy level

2. **Add Configuration XML** (if used):
   - Click "Add Content"
   - Upload configuration XML
   - Zoneable Key: (optional, for multi-zone config)
   - Type: Configuration Data
   - Zone: Assign as needed

3. **Add Additional Files**:
   - Upload dependent DLLs (set DiskFile names if needed)
   - Upload images, templates, etc.
   - Set appropriate types and zones

### Zoneable Configuration

Extensions support hierarchical configuration:

- **Enterprise Level**: Global defaults
- **Property Level**: Property-specific overrides
- **RVC Level**: Revenue center specific

Configuration is read from most specific to least specific zone.

## Extension Instantiation Lifecycle

### Critical Understanding: When Extensions Update

**IMPORTANT - Common Source of Errors:**

```
┌─────────────────────────────────────────────────────────┐
│ Simphony CONTINUOUSLY downloads data from EMC           │
│ - Configuration updates                                 │
│ - Data updates                                          │
│ - Extension Application Content (including updated DLL) │
└─────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────┐
│ BUT: DLL is ONLY written to disk and instantiated      │
│      when Simphony STARTS                               │
└─────────────────────────────────────────────────────────┘
```

**This means:**
- Upload new DLL to EMC ✓
- Simphony downloads it ✓
- **Extension still runs OLD version** ❌
- Must **restart Simphony** to load new DLL ✓

**Why Version Script is Mandatory:**
- Verify which version is actually running
- Avoid debugging "phantom issues" with old code
- Confirm deployment succeeded

### Startup Sequence

1. **Simphony Starts**
   - Connects to EMC (Oracle Cloud)
   - Downloads Extension Application Content

2. **DLL Deployment**
   - Finds Extension Applications marked Active
   - Downloads DLL content (based on DiskFile name)
   - Writes DLL to local disk
   - Identifies DLLs with `IExtensibilityAssemblyFactory`

3. **Factory Instantiation**
   - Calls `IExtensibilityAssemblyFactory.Create(IExecutionContext)`
   - Creates `SimphonyExtensibilityApplication` instance
   - Executes constructor (DI setup, event registration)

4. **Extension Ready**
   - Extension is now active and responding to events
   - Buttons can call scripts via `CallFunc`

### Runtime Behavior

Once instantiated, the extension:
- Responds to button clicks via `CallFunc`
- Handles registered Simphony events
- Runs background services (if configured)
- Accesses configuration via `IConfigurationClient`

**Important:** The same DLL instance runs until Simphony restarts.

## Development Deployment (PostBuildEvent)

### Automatic Local Deployment

During development, use PostBuildEvent to automatically deploy to local Simphony:

```xml
<PropertyGroup>
  <PostBuildEvent>
    xcopy "$(TargetPath)" "C:\Micros\Simphony\ServiceHost\NetBackup\" /Y /D
    xcopy "$(TargetDir)$(TargetName).pdb" "C:\Micros\Simphony\ServiceHost\NetBackup\" /Y /D
    xcopy "$(TargetDir)$(TargetName).xml" "C:\Micros\Simphony\ServiceHost\NetBackup\" /Y /D
  </PostBuildEvent>
</PropertyGroup>
```

**What this does:**
- Copies DLL to Simphony directory after build
- Copies PDB (for debugging symbols)
- Copies XML (configuration)
- `/Y` - Overwrite without prompting
- `/D` - Only copy if source is newer

**Development Workflow:**
1. Make code changes
2. Build project (F6)
3. PostBuildEvent copies DLL automatically
4. **Restart Simphony** to load new DLL
5. Test changes

**Note:** Even with PostBuildEvent, you must restart Simphony to load the updated DLL.

## Production Deployment

### Deployment Process

1. **Build Release Version**
   - Set configuration to Release
   - Build solution
   - Verify version number in AssemblyInfo

2. **Upload to EMC**
   - Open EMC
   - Navigate to your Extension Application
   - Update Primary DLL content
   - **Ensure DiskFile name is set**
   - Upload updated configuration XML (if changed)
   - Save changes

3. **Distribution to Workstations**
   - EMC pushes changes to Oracle Cloud
   - Workstations continuously sync
   - Download updated Extension Application Content
   - **DLL sits in cache, NOT yet active**

4. **Activation (Simphony Restart)**
   - Workstations must restart Simphony
   - New DLL written to disk
   - New version instantiated
   - Old version unloaded

### Multi-Workstation Deployment

**Coordinated Restart Strategy:**

**Option 1: Scheduled Restart**
- Schedule maintenance window
- Communicate to staff
- Restart all workstations simultaneously
- Verify deployment with Version script

**Option 2: Rolling Restart**
- Restart workstations in groups
- Verify each group before proceeding
- Minimal operational disruption
- Some workstations run old version temporarily

**Option 3: End-of-Day Restart**
- Upload new version during business hours
- Restart all workstations at close of business
- New version active for next day
- Minimal disruption

### Version Management

**Best Practices:**

1. **Increment Version Numbers**
   - Update AssemblyInfo.cs before each release
   - Use semantic versioning (Major.Minor.Patch.Build)
   - Document changes in release notes

2. **Version Verification**
   - Click Version button on each workstation
   - Confirm expected version is running
   - Document actual versions in deployment log

3. **Rollback Plan**
   - Keep previous DLL version in EMC history
   - Can revert by re-uploading old version
   - Still requires Simphony restart

## Common Deployment Issues

### Issue 1: Extension Not Instantiating

**Symptoms:**
- Extension doesn't load
- No errors, just silent failure
- Buttons don't work

**Causes:**
- ✅ **DiskFile name not set in EMC** (MOST COMMON)
- Missing `IExtensibilityAssemblyFactory` implementation
- Exception in constructor
- Wrong .NET Framework version

**Solution:**
1. Verify DiskFile name is set in EMC
2. Check EMC logs for errors
3. Verify `ApplicationFactory` class exists
4. Check Simphony logs for exceptions

### Issue 2: Old Version Still Running

**Symptoms:**
- Uploaded new DLL to EMC
- Changes not reflected
- Version script shows old version

**Cause:**
- **Simphony not restarted after upload** (VERY COMMON)

**Solution:**
- Restart Simphony on workstation
- Verify with Version script
- Remember: Download ≠ Instantiation

### Issue 3: Configuration Not Updating

**Symptoms:**
- Updated XML in EMC
- Extension still uses old configuration

**Causes:**
- Configuration cached (10-minute cache in AbstractScript)
- Reading wrong zone
- XML not uploaded correctly

**Solution:**
1. Wait 10 minutes for cache expiry OR
2. Restart Simphony to force reload OR
3. Use RefreshConfig() in script
4. Verify XML uploaded to correct zone

### Issue 4: Missing Dependent DLLs

**Symptoms:**
- Extension loads but crashes
- `FileNotFoundException` for dependent DLL
- Features using third-party libraries fail

**Cause:**
- Dependent DLLs not uploaded to EMC
- Missing DiskFile names on dependencies

**Solution:**
1. Upload all dependent DLLs as Extension Application Content
2. Set DiskFile names for each DLL
3. Restart Simphony
4. Verify all DLLs written to disk

### Issue 5: Multi-Workstation Version Mismatch

**Symptoms:**
- Some workstations work, others don't
- Inconsistent behavior across terminals
- Version script shows different versions

**Cause:**
- Workstations not restarted uniformly
- Some workstations offline during deployment

**Solution:**
1. Create restart checklist
2. Verify each workstation individually
3. Use Version script on all terminals
4. Document actual versions per workstation

## Deployment Checklist

### Pre-Deployment

- [ ] Code changes tested in development
- [ ] Version number incremented in AssemblyInfo
- [ ] Release build completed successfully
- [ ] Configuration XML updated (if needed)
- [ ] All dependent DLLs identified
- [ ] Deployment window scheduled
- [ ] Staff notified of restart
- [ ] Rollback plan documented

### Deployment

- [ ] Login to EMC
- [ ] Navigate to Extension Application
- [ ] Upload new DLL version
- [ ] **Verify DiskFile name is set**
- [ ] Upload configuration XML (if changed)
- [ ] Upload dependent DLLs (if changed)
- [ ] Verify all DiskFile names
- [ ] Save changes in EMC
- [ ] Wait for sync to complete

### Post-Deployment

- [ ] Restart Simphony on all workstations
- [ ] Click Version button on each terminal
- [ ] Verify expected version displayed
- [ ] Test core functionality
- [ ] Test new features/fixes
- [ ] Document actual versions deployed
- [ ] Update deployment log
- [ ] Notify staff deployment complete

## Troubleshooting Tools

### Version Script (Essential)

```csharp
public class Version : AbstractScript
{
    private readonly IOpsContextClient _opsContextClient;

    public Version(IOpsContextClient opsContextClient)
    {
        _opsContextClient = opsContextClient;
    }

    public void ShowVersion()
    {
        _opsContextClient.ShowMessage(VersionHelper.NameAndVersion);
    }
}
```

**Usage:**
- Create button calling Version script
- Click after every deployment
- Verify version matches expected
- Document version in deployment log

### Log Files

Check Simphony logs for:
- Extension load errors
- Constructor exceptions
- Event handler errors
- Runtime exceptions

**Common log locations:**
- `C:\Micros\Simphony\Logs\`
- Check for extension name in logs
- Look for exceptions around startup time

### EMC Verification

Verify in EMC:
- Extension Application Active ✓
- DiskFile names set on all DLLs ✓
- Content uploaded successfully ✓
- Correct zones assigned ✓

## Best Practices

### 1. Always Set DiskFile Names

```
DLL uploads:
✅ YourExtension.dll → DiskFile: "YourExtension.dll"
✅ ThirdParty.dll → DiskFile: "ThirdParty.dll"
❌ YourExtension.dll → DiskFile: (empty) - WILL NOT LOAD!
```

### 2. Always Include Version Script

```
Every extension must have:
✅ Version.cs script
✅ VersionHelper.cs helper
✅ Button to call Version script
✅ Button on every page template
```

### 3. Always Restart After Deployment

```
Deployment workflow:
1. Upload new DLL to EMC ✓
2. Wait for sync complete ✓
3. RESTART SIMPHONY ✓ ← CRITICAL
4. Verify with Version script ✓
```

### 4. Document Every Deployment

```
Deployment log should include:
- Date/Time of deployment
- Version number deployed
- Workstations restarted
- Actual versions verified
- Issues encountered
- Resolution steps
```

### 5. Test Before Production

```
Development → Test → Production

Development:
- PostBuildEvent deployment
- Local Simphony instance
- Rapid iteration

Test:
- EMC deployment to test workstations
- Full restart verification
- Version script verification

Production:
- Scheduled deployment window
- Coordinated restart
- Version verification
- Rollback plan ready
```

### 6. Use Semantic Versioning

```
Format: Major.Minor.Patch.Build

Examples:
1.0.0.0 - Initial release
1.0.1.0 - Bug fix (backward compatible)
1.1.0.0 - New feature (backward compatible)
2.0.0.0 - Breaking change (not backward compatible)
```

### 7. Maintain Deployment History

Keep track of:
- Version numbers deployed
- Deployment dates
- Which workstations updated
- Issues encountered
- Rollback events
- Configuration changes

## Security Considerations

### Code Signing (Optional)

Consider signing DLLs:
- Prevents tampering
- Verifies publisher
- Provides trust chain

### Access Control

- Limit EMC access to authorized personnel
- Use separate accounts for dev/test/prod
- Audit extension uploads
- Review extension changes before deployment

### Testing

- Never deploy untested code to production
- Use test environment for verification
- Test rollback procedure periodically
- Document test results

## Summary

**Critical Deployment Rules:**

1. **DiskFile name is MANDATORY** - Extension won't load without it
2. **Restart Simphony to activate** - DLL download ≠ DLL instantiation
3. **Verify with Version script** - Always confirm actual version running
4. **Test before production** - Use dev/test environments
5. **Document everything** - Track versions, dates, issues

**Common Mistakes to Avoid:**

- ❌ Forgetting to set DiskFile name
- ❌ Not restarting Simphony after upload
- ❌ Assuming download = activation
- ❌ Not verifying with Version script
- ❌ Deploying untested code
- ❌ Not having rollback plan

**Deployment Success:**

- ✅ DiskFile names set on all DLLs
- ✅ All workstations restarted
- ✅ Version verified on every terminal
- ✅ Core functionality tested
- ✅ Deployment documented
- ✅ Staff notified

The deployment lifecycle in Simphony is simple but has critical gotchas. Understanding the distinction between **downloading extension content** (continuous) and **instantiating the DLL** (only at startup) is essential to successful deployments and avoiding frustrating troubleshooting sessions.
