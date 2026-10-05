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
    }
}
