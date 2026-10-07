-- AG-UI / CopilotKit chat history: one row per request/response turn.
-- SessionId (Guid) comes from the X-Session-Id header sent by the chat widget.
CREATE TABLE tbl_agent_chat_sessions (
    Id           BIGINT IDENTITY(1,1) PRIMARY KEY,
    SessionId    UNIQUEIDENTIFIER NOT NULL,
    Request      NVARCHAR(MAX)    NOT NULL,
    Response     NVARCHAR(MAX)    NULL,
    ToolTrace    NVARCHAR(MAX)    NULL,
    InputTokens  INT              NULL,
    OutputTokens INT              NULL,
    IsActive     BIT              NOT NULL DEFAULT 1,
    CreatedBy    INT              NOT NULL,
    CreatedDate  DATETIME         NOT NULL DEFAULT GETUTCDATE()
);
GO

CREATE INDEX IX_tbl_agent_chat_sessions_SessionId ON tbl_agent_chat_sessions (SessionId, CreatedBy);
GO
