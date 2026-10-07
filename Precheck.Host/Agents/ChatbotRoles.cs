namespace Precheck.Host.Agents
{
    // Matches the actual UserRole table: 1=Admin, 2=Planner, 3=Store, 4=QC.
    public static class ChatbotRoles
    {
        public static string GetDomainName(int roleId) => roleId switch
        {
            1 => "Admin",
            3 => "Store",
            4 => "QC",
            _ => "Planner" // covers 2 (Planner) and any unrecognized role
        };

        public static string GetDomainDescription(int roleId) => roleId switch
        {
            1 => "Admin: full oversight across Production Order status and tracking, QR Code and " +
                 "IR/MSN lookups, and Precheck status - suggestions aren't limited to one domain and " +
                 "can cover any of these areas.",
            3 => "Store: Precheck status, pending prechecks, and component availability",
            4 => "QC: QR Code tracking and IR/MSN number lookups",
            _ => "Planner: Production Order status and tracking"
        };

        // Starter questions shown when a chat is opened with no message.
        public static string[] GetStarterQuestions(int roleId) => roleId switch
        {
            1 => new[]
            {
                "Do you want to see the total production orders?",
                "Do you want to check the precheck status of a production order?",
                "Do you want to look up a QR code or IR/MSN number?"
            },
            3 => new[]
            {
                "Do you want to see the pending prechecks?",
                "Do you want to check the precheck status of a production order?",
                "Do you want to check component availability?"
            },
            4 => new[]
            {
                "Do you want to look up the details of a QR code?",
                "Do you want to find the IR number for a component?",
                "Do you want to find the MSN number for a component?"
            },
            _ => new[]
            {
                "Do you want to see the total production orders?",
                "Do you want to see the pending production orders?",
                "Do you want to track the status of a production order?"
            }
        };
    }
}
