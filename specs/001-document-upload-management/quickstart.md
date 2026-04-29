# Quickstart: Document Upload and Management

**Feature**: 001-document-upload-management  
**Branch**: `001-document-upload-management`

## Prerequisites

- .NET 8.0 SDK installed
- SQL Server LocalDB available (`sqllocaldb info` should list an instance)
- ContosoDashboard solution opens and runs without errors on `main`

---

## Step 1: Ensure clean database state

If you have run the app before with any previous (broken) document upload attempts, drop and recreate the database to remove orphaned records:

```powershell
# Option A — via EF Core tools (if installed)
cd ContosoDashboard
dotnet ef database drop --force

# Option B — via sqllocaldb
sqllocaldb stop mssqllocaldb
sqllocaldb delete mssqllocaldb
# Database recreates automatically on next app start
```

---

## Step 2: Run the application

```powershell
cd ContosoDashboard
dotnet run
```

Navigate to `https://localhost:5001` (or the port shown in the terminal).  
Log in with any seeded user (e.g., `admin@contoso.com`).

---

## Step 3: Upload your first document

1. Click **Documents** in the left navigation.
2. Click **Upload Document** — the upload modal opens.
3. Select any PDF or image file under 25 MB.
4. Fill in **Title** (required) and select a **Category** (required).
5. Optionally associate the document with a project.
6. Click **Upload**.
7. The document appears in the **My Documents** tab.

*The upload should complete within a few seconds for local files.*

---

## Step 4: Browse, sort, and filter

1. In **My Documents**, use the sort controls (Title / Upload Date / Category / File Size).
2. Use the **Filter** dropdowns to narrow by Category or Project.
3. Upload 2–3 more documents with different categories to test filtering.

---

## Step 5: Download and preview

1. Click the download icon next to any document — the file is saved to your Downloads folder.
2. For a PDF or image, click the preview icon — the file opens inline in the browser.

---

## Step 6: Share a document

1. Click the share icon next to a document.
2. Search for another seeded user and click **Share**.
3. Log out and log in as that user.
4. Navigate to **Documents → Shared with Me** tab — the document should appear.
5. The recipient also has an unread notification about the shared document.

---

## Step 7: Attach a document to a task

1. Navigate to **My Tasks** and open any task.
2. Click **Attach Document**.
3. Select an existing document or use the upload modal to upload a new one.
4. The document appears in the task's **Related Documents** section.

---

## Step 8: Verify dashboard integration

1. Navigate to the **Dashboard** home page.
2. The **Recent Documents** widget shows your 5 most recently uploaded documents.
3. The **Documents** summary card shows your total accessible document count.

---

## Step 9: Test deletion and share revocation

1. Share a document with another user (Step 6).
2. Log back in as the document owner.
3. Delete the document — confirm the deletion prompt.
4. Log in as the recipient — the document no longer appears in **Shared with Me**.
5. The recipient has an in-app notification: "A document shared with you has been removed."

---

## Step 10: Admin audit log (Administrator role only)

1. Log in as `admin@contoso.com`.
2. Navigate to **Documents → Activity Log**.
3. All upload, download, delete, and share events from previous steps appear in the log.

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Duplicate key violation on upload | Drop and recreate the database (Step 1) |
| "File not found" on download | Check that `AppData/uploads/` exists under the project root; restart the app |
| Upload modal doesn't reset after success | Verify `@key` attribute on `InputFile` component in `Documents.razor` |
| Preview fails for PDF | Confirm browser supports inline PDF rendering; try a different browser |
| Missing claims / authorization failures | Ensure login flow sets NameIdentifier, Name, Email, Role, and Department claims in `Login.cshtml.cs` |
