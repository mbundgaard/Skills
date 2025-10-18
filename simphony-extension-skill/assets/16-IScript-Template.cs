namespace YourNamespace.Contracts
{
    /// <summary>
    /// Universal script interface for all Simphony Extension Application business logic.
    /// ALL scripts must implement this interface.
    /// Place in: Contracts/IScript.cs
    ///
    /// IMPORTANT: Do NOT implement this interface directly.
    /// Always inherit from AbstractScript base class instead.
    /// </summary>
    public interface IScript
    {
        /// <summary>
        /// Execute a script function.
        /// Called from SimphonyExtensibilityApplication.CallFunc when buttons are clicked.
        /// </summary>
        /// <param name="functionName">Name of the function to execute (from button configuration)</param>
        /// <param name="argument">Argument passed from button configuration</param>
        void Execute(string functionName, string argument);
    }
}
