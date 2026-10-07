namespace Precheck.Repository.Queries
{
    public static class ChatbotQueries
    {
        #region GET_NEXT_SESSION_ID

        public static readonly string GET_NEXT_SESSION_ID = @"
            SELECT ISNULL(MAX(SessionId), 0) + 1 FROM ChatSession";

        #endregion

        #region SESSION_BELONGS_TO_USER

        public static readonly string SESSION_BELONGS_TO_USER = @"
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM ChatSession WHERE SessionId = @SessionId AND UserId = @UserId
            ) THEN 1 ELSE 0 END";

        #endregion

        #region INSERT_CHAT_HISTORY

        public static readonly string INSERT_CHAT_HISTORY = @"
            INSERT INTO ChatSession (UserId, SessionId, Request, Response, CreatedDate)
            VALUES (@UserId, @SessionId, @Request, @Response, GETUTCDATE())";

        #endregion

        #region GET_HISTORY_BY_SESSION

        public static readonly string GET_HISTORY_BY_SESSION = @"
            SELECT Id, UserId, SessionId, Request, Response, CreatedDate
            FROM ChatSession
            WHERE SessionId = @SessionId
            ORDER BY Id ASC";

        #endregion

        #region GET_PREVIOUS_SESSION_HISTORY

        // Keyset (cursor) page of the user's most recent session older than @BeforeSessionId, newest exchange first.
        public static readonly string GET_PREVIOUS_SESSION_HISTORY = @"
            SELECT TOP (@PageSize) Id, UserId, SessionId, Request, Response, CreatedDate
            FROM ChatSession
            WHERE UserId = @UserId
              AND SessionId = (
                  SELECT MAX(SessionId) FROM ChatSession
                  WHERE UserId = @UserId AND SessionId < @BeforeSessionId)
              AND (@Cursor IS NULL OR Id < @Cursor)
            ORDER BY Id DESC";

        #endregion

        #region GET_LAST_SESSION_MESSAGES

        // Last @Count rows of one session, restricted to the owner, newest first.
        public static readonly string GET_LAST_SESSION_MESSAGES = @"
            SELECT TOP (@Count) Id, UserId, SessionId, Request, Response, CreatedDate
            FROM ChatSession
            WHERE SessionId = @SessionId AND UserId = @UserId
            ORDER BY Id DESC";

        #endregion
    }
}
