using System.Collections.Generic;

namespace YourNamespace.Contracts.Clients
{
    /// <summary>
    /// Interface for interacting with the Simphony OpsContext.
    /// Start with these 6 essential methods and add more as your extension needs grow.
    /// Place in: Contracts/Clients/IOpsContextClient.cs
    /// </summary>
    public interface IOpsContextClient
    {
        /// <summary>
        /// Display an informational message to the user.
        /// </summary>
        void ShowMessage(string message);

        /// <summary>
        /// Display an error message to the user.
        /// </summary>
        void ShowError(string message);

        /// <summary>
        /// Ask the user a yes/no question.
        /// </summary>
        /// <returns>True if user clicked Yes, False if No</returns>
        bool AskQuestion(string message);

        /// <summary>
        /// Request a decimal amount from the user.
        /// </summary>
        /// <returns>The amount entered, or null if cancelled</returns>
        decimal? GetAmountInput(string message);

        /// <summary>
        /// Request text input from the user.
        /// </summary>
        /// <returns>The text entered, or empty string if cancelled</returns>
        string GetTextInput(string message);

        /// <summary>
        /// Display a list for the user to select from.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="prompt">Dialog prompt message</param>
        /// <param name="items">Array of items to choose from</param>
        /// <returns>Index of selected item, or null if cancelled</returns>
        int? SelectFromList(string title, string prompt, string[] items);

        // EXPAND AS NEEDED:
        // Add methods for your specific extension requirements:
        // - Check operations (GetCheckInfo, GetMenuItemsOnCheck, etc.)
        // - Posting operations (PostTender, PostServiceCharge, etc.)
        // - Employee operations (GetEmployeeNumber, GetAuthorizingEmployee, etc.)
        // - Extensibility data (AddUpdateExtensibilityData, ReadExtensibilityData, etc.)
        // - Network messages (SendNetworkMessage)
        // - Printing (Print, GetGuestCheckPrint)
        // See input examples for full method signatures
    }
}
