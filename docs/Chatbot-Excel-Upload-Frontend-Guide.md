# Chatbot Excel Upload – Frontend Guide

**Audience:** frontend / upload team.
**Purpose:** how to let a user attach an Excel file in the chatbot, what to send, and which APIs get called.

## 1. How it works (overview)

The chatbot (LLM) never receives the file itself. The file goes to the server first, and the chat only carries a short reference to it.

1. User attaches an Excel file in the chat.
2. Frontend uploads it to `POST /api/Chatbot/UploadFile` and receives a `fileId`.
3. Frontend sends the user's chat message to the normal chatbot endpoint, with the returned `attachmentNote` included in the message text.
4. The chatbot replies asking what the user wants to do with the file (options listed in section 5).
5. The user answers in chat (e.g. "import production orders").
6. The chatbot calls the matching tool on the server, using the `fileId`, and replies with the result.

Nothing is read or imported until step 6. **The frontend never calls the import APIs itself for chat uploads.**

## 2. API the frontend calls: Upload File

`POST /api/Chatbot/UploadFile`

| Item | Value |
|---|---|
| Auth | Required – same `Authorization: Bearer <token>` header as other APIs |
| Content-Type | `multipart/form-data` |
| Form field | `file` (exactly this name) – one file per request |
| Allowed types | `.xlsx` or `.xls` |
| Max size | 10 MB |
| File name | Up to 100 characters; letters, digits, space, `-`, `_`, `.`, `(`, `)` only |

### Success response (200)

```json
{
  "fileId": "3f2b8c1e9a4d4c6f8e1b2a3c4d5e6f70",
  "fileName": "production_orders.xlsx",
  "sizeBytes": 18432,
  "attachmentNote": "[Attached file \"production_orders.xlsx\" - fileId: 3f2b8c1e9a4d4c6f8e1b2a3c4d5e6f70]"
}
```

### Error responses

| Status | Body | Cause |
|---|---|---|
| 400 | `{ "message": "No file uploaded" }` | Empty or missing `file` |
| 400 | `{ "message": "Invalid file format. Please upload an Excel file (.xlsx or .xls)" }` | Wrong extension |
| 400 | `{ "message": "File size must not exceed 10 MB." }` | Too large |
| 400 | `{ "message": "The file is not a valid Excel file." }` | Renamed non-Excel file |
| 400 | `{ "message": "Invalid file name. ..." }` | Unsafe file name |
| 401 | – | Missing / expired token |
| 500 | `{ "message": "Unable to save the file." }` | Server error |

Show the `message` to the user on a 400.

## 3. What to send in the chat message

Send the user's chat message as usual, and **include `attachmentNote` exactly as returned** in the message text. For example:

```
[Attached file "production_orders.xlsx" - fileId: 3f2b8c1e9a4d4c6f8e1b2a3c4d5e6f70]
```

Rules:
- Do not edit, shorten or reformat the note. The chatbot reads the `fileId` from it.
- The user can add their own text after the note (e.g. "please import these"). If they add nothing, send the note alone.
- Do not send the file bytes, a file path or base64 in the chat message.
- To attach two files (master data), upload each file separately and include both notes in the same message.
- The chat endpoint is the existing chatbot (AG-UI / CopilotKit) endpoint – no change to its URL or payload shape.

## 4. Expected chatbot behaviour

After a new attachment the chatbot will **not** act. It asks what the user wants to do and lists the options. The user's reply is the instruction, so there is no extra confirm step. Show the chatbot's replies as normal chat messages.

At the end the chatbot reports counts and lists failed rows (up to 30), for example "Imported 18 of 20 rows" followed by the failures.

## 5. Which action gets called

| User wants to… | Server tool called | Same logic as existing API | File type | Expected sheet layout |
|---|---|---|---|---|
| Import new Production Orders | `import_production_orders_from_excel` | `POST /api/ProductionOrder/Upload` | .xlsx (.xls is not readable) | Row 1 = header. Columns in order: PO No, Project Code, Project Description, Item Code, Item Description, Start ID Number (e.g. `GA0153`), Quantity, MRIR No, MIN, Status, Build No, Snag Sheet No |
| Update MIN / Status of existing Production Orders | `update_production_order_min_status_from_excel` | `POST /api/ProductionOrder/UpdateMinStatus` | .xlsx (.xls is not readable) | Row 1 = header. PO No in column 1, MIN in column 9, Status in column 10 |
| Make Precheck in bulk | `make_precheck_from_excel` | `POST /api/Precheck/MakePrecheckFromExcel` | .xlsx (.xls is not readable) | Use the template from `GET /api/Precheck/BulkPrecheckTemplate`. Row 3 = header, data from row 4. Needs Drawing Number (col 2), Qty (6), Parent Drawing (7), ProductionOrderNumber (9), IdNumber (10), QRCodeNumber (11) |
| Bulk Store In QR codes | `bulk_store_in_from_excel` | `POST /api/QRCode/BulkStoreInFromExcel` | .xlsx (.xls is not readable) | Use the template from `GET /api/QRCode/BulkStoreInTemplate`. Row 1 = header (Sr. No. \| QrCodeNumber), QR codes in column 2 from row 2 |
| Generate standard QR codes | `run_standard_qr_generation_from_excel` | `POST /api/Script/UploadExcel` then `POST /api/Script/RunSTDQRGeneration` | **.xlsx only** | Template: `GET /api/Script/DownloadTemplate/stdqrgeneration` |
| Import QR codes with IR/MSN | `run_qr_code_import_from_excel` | `POST /api/Script/UploadExcel` then `POST /api/Script/RunQRCodeImport` | **.xlsx only** | Template: `GET /api/Script/DownloadTemplate/qrcodeimport` |
| Upload master data | `run_master_data_upload` | `POST /api/Script/UploadMasterDataExcel` then `POST /api/Script/RunMasterData` | **.xlsx only, TWO files** | File 1 (drawing-assembly): `.../DownloadTemplate/masterdata1`. File 2 (drawing): `.../DownloadTemplate/masterdata2` |

The "Same logic as existing API" column is for reference only. The chat flow does not call those endpoints from the frontend, it calls the tools on the server. The existing endpoints keep working for the normal (non-chat) upload screens.

## 6. Things to handle in the UI

- **Expiry:** an uploaded file is kept for about 2 hours and is deleted once an action has used it. If the chatbot says the file was not found or expired, ask the user to attach it again (upload again and send the new note).
- **One file, one action:** each file is consumed by the action. To run a second action on the same data, the user attaches the file again.
- **Script actions and master data:** these run an external program and can take several minutes (the server stops them after 10 minutes). Show a loading state in the chat while waiting.
- **`.xls` files:** the upload endpoint accepts them, but the server can only read `.xlsx` content, so an `.xls` file will fail when processed. Steer users to `.xlsx`.
- **Templates:** offer the template download links from section 5 next to the attach button so users start with the right layout.
- **Validation:** do a light client-side check (extension and 10 MB) to give instant feedback. The server validates again.

## 7. Errors in the file

The import itself checks every row and reports what is wrong, using the same messages as the existing upload APIs. In chat, the chatbot shows those messages to the user as a list, exactly as the API words them, and tells the user to correct those rows and attach the file again.

The frontend does nothing extra: the errors arrive as normal chat messages. Examples of what the user may see:

> Imported 18 of 20 rows.
> - Row 'PO-1007': Invalid Start ID format: '153GA'
> - Row 'PO-1012': Production Order already exists

Notes:
- At most 30 failed rows are listed per run; the chatbot is told how many more were not shown.
- A whole-file problem (for example "No data rows found") is shown as a single message.
- Rows that failed are not imported; rows that passed are. To retry, the user fixes only the failed rows, saves the file and attaches it again (a new upload and a new `fileId`).
- For the script actions (standard QR, QR import, master data) the chatbot shows the script's own output, including any errors it printed.

## 8. Example end-to-end

1. User attaches `po_update.xlsx`.
2. Frontend: `POST /api/Chatbot/UploadFile` (form field `file`) returns `fileId: 9c1d...`.
3. Frontend sends the chat message:
   `[Attached file "po_update.xlsx" - fileId: 9c1d...]`
4. Chatbot: "What would you like to do with this file?" followed by the seven options.
5. User: "update min and status".
6. Server runs `update_production_order_min_status_from_excel`.
7. Chatbot: "Processed 40 rows. Updated 38. Not found: 2." followed by the two missing PO numbers.
