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
        // Each one is sent as the user's own message when clicked, so it must be a self-contained
        // request the agent can answer directly - not a question addressed to the user.
        public static string[] GetStarterQuestions(int roleId) => roleId switch
        {
            1 => new[]
            {
                "How many production orders are there?",
                "List the pending prechecks",
                "Give me the available QR codes"
            },
            3 => new[]
            {
                "List the pending prechecks",
                "Which production orders are partially completed?",
                "Give me the available components"
            },
            4 => new[]
            {
                "Give me the available QR codes",
                "How many QR codes are consumed?",
                "Tell me the QR code summary"
            },
            _ => new[]
            {
                "How many production orders are there?",
                "List the pending production orders",
                "Give me the completed production orders"
            }
        };
    }
}
