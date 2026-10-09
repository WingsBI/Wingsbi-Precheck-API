# Chatbot Excel Upload – Frontend Guide

**Audience:** frontend / upload team.
**Purpose:** how to let a user attach an Excel file in the chatbot, what to send, and which APIs get called.

## 1. How it works (overview)

The chatbot (LLM) never receives the file itself. The file goes to the server first, and the chat only carries a short reference to it.

1. User attaches an Excel file in the chat.
2. Frontend uploads it to `POST /api/Chatbot/UploadFile` and receives a `fileId`.
3. Frontend sends the user's chat message to the normal chatbot endpoint, with the returned `attachmentNote` included in the message text.
4. The chatbot works out what the file is from its layout and runs the right action straight away. The user is not asked what to do.
5. The chatbot replies with what it detected and the result (counts and any failed rows).
6. Only when it cannot be sure (section 4) does it ask the user a question first.

Nothing is read or imported until step 4. **The frontend never calls the import APIs itself for chat uploads.**

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

After a new attachment the chatbot **detects the file type from its header layout and runs the action immediately**, with no question and no confirm step. Its reply starts with what it detected, e.g. "Detected: Bulk Store In sheet", then the counts and any failed rows (up to 30), e.g. "Imported 18 of 20 rows" followed by the failures. Show its replies as normal chat messages.

| File | What happens |
|---|---|
| Precheck BOM sheet, Bulk Store In sheet, QR code sample | Detected and run automatically |
| Production Order template | The same template is used for two actions, so the data decides: none of the POs exist yet -> **import**; every row (PO + series + Start ID) already exists -> **update MIN/Status**; a mix of both -> the chatbot **asks** which one the user wants |
| Master data | Needs two files (drawing-assembly and drawing). With one file the chatbot says which one is missing and waits; once the second file is attached it runs. The two files can be attached in one message or in two separate messages |
| Unrecognised layout, corrupt or `.xls` file | Nothing is run. The chatbot says so, lists the supported templates and asks the user to attach again |

The user can still tell the chatbot explicitly what to do with a file in the same message (e.g. "update MIN/status from this file"), and it will do that instead of auto-detecting.

## 5. Which action gets called (reference)

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
4. Server detects a Production Order sheet whose POs all already exist, and runs the MIN/Status update.
5. Chatbot: "Detected: Production Order sheet - updated MIN/Status. Processed 40 rows. Updated 38. Not found: 2." followed by the two missing PO numbers.
