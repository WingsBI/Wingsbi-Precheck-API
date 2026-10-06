-- Single table for chatbot history: one row per request/response exchange.
-- SessionId groups the exchanges of one conversation; a new one is MAX(SessionId) + 1.
CREATE TABLE ChatSession (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    UserId      INT            NOT NULL,
    SessionId   INT            NOT NULL,
    Request     NVARCHAR(MAX)  NOT NULL,
    Response    NVARCHAR(MAX)  NOT NULL,
    CreatedDate DATETIME       NOT NULL DEFAULT GETUTCDATE()
);
GO

CREATE INDEX IX_ChatSession_SessionId ON ChatSession (SessionId, UserId);
GO

