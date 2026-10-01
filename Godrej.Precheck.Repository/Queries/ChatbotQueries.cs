namespace Godrej.Precheck.Repository.Queries
{
    public static class ChatbotQueries
    {
        #region INSERT_CHAT_SESSION

        public static readonly string INSERT_CHAT_SESSION = @"
            INSERT INTO ChatSession (UserId, Title, CreatedDate)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @Title, GETDATE())";

        #endregion

        #region SESSION_BELONGS_TO_USER

        public static readonly string SESSION_BELONGS_TO_USER = @"
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM ChatSession WHERE Id = @SessionId AND UserId = @UserId
            ) THEN 1 ELSE 0 END";

        #endregion

        #region GET_SESSION_STATE

        public static readonly string GET_SESSION_STATE = @"
            SELECT SessionState FROM ChatSession WHERE Id = @SessionId";

        #endregion

        #region UPDATE_SESSION_STATE

        public static readonly string UPDATE_SESSION_STATE = @"
            UPDATE ChatSession SET SessionState = @SessionState WHERE Id = @SessionId";

        #endregion

        #region INSERT_CHAT_MESSAGE

        public static readonly string INSERT_CHAT_MESSAGE = @"
            INSERT INTO ChatMessage (SessionId, Role, Content, ToolCalled, CreatedDate)
            VALUES (@SessionId, @Role, @Content, @ToolCalled, GETDATE())";

        #endregion

        #region GET_MESSAGES_BY_SESSION

        public static readonly string GET_MESSAGES_BY_SESSION = @"
            SELECT Id, SessionId, Role, Content, ToolCalled, CreatedDate
            FROM ChatMessage
            WHERE SessionId = @SessionId
            ORDER BY CreatedDate ASC, Id ASC";

        #endregion
    }
}
