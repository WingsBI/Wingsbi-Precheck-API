namespace Precheck.Repository.Queries
{
    public static class AgentChatQueries
    {
        #region INSERT_MESSAGE

        public static readonly string INSERT_MESSAGE = @"
            INSERT INTO tbl_agent_chat_sessions (SessionId, Request, Response, ToolTrace, InputTokens, OutputTokens, IsActive, CreatedBy, CreatedDate)
            OUTPUT INSERTED.Id
            VALUES (@SessionId, @Request, @Response, @ToolTrace, @InputTokens, @OutputTokens, 1, @CreatedBy, GETUTCDATE())";

        #endregion

        #region SELECT_SESSION_PAGE

        // Newest first; the service reverses a page into reading order. BeforeId = cursor (id of the oldest turn already loaded).
        public static readonly string SELECT_SESSION_PAGE = @"
            SELECT TOP (@Limit) Id, Request, Response, InputTokens, OutputTokens, CreatedDate
            FROM tbl_agent_chat_sessions
            WHERE SessionId = @SessionId
              AND CreatedBy = @UserId
              AND IsActive = 1
              AND (@BeforeId IS NULL OR Id < @BeforeId)
            ORDER BY Id DESC";

        #endregion
    }
}
