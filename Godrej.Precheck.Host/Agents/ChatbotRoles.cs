namespace Godrej.Precheck.Host.Agents
{
    public static class ChatbotRoles
    {
        public static string GetDomainName(int roleId) => roleId switch
        {
            2 => "QC",
            3 => "Store",
            _ => "Planner"
        };

        public static string GetDomainDescription(int roleId) => roleId switch
        {
            2 => "QC: QR Code tracking and IR/MSN number lookups",
            3 => "Store: Precheck status, pending prechecks, and component availability",
            _ => "Planner: Production Order status and tracking"
        };
    }
}
