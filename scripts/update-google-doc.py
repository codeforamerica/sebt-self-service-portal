"""
Appends weekly release notes to a shared Google Doc.
Called by the GitHub Actions workflow; reads env vars set by the workflow.
 
Required env vars:
  GOOGLE_SERVICE_ACCOUNT_JSON  – full JSON string of the service account key
  GOOGLE_DOC_ID                – the document ID from the Google Doc URL
  NOTES                        – release notes markdown text
"""
 
import json
import os
import sys
 
from google.oauth2 import service_account
from googleapiclient.discovery import build
 
# ── Config ────────────────────────────────────────────────────────────────────
 
SCOPES = ["https://www.googleapis.com/auth/documents"]
 
sa_json = os.environ.get("GOOGLE_SERVICE_ACCOUNT_JSON")
doc_id  = os.environ.get("GOOGLE_DOC_ID")
notes   = os.environ.get("NOTES", "")
 
if not sa_json or not doc_id or not notes:
    print("ERROR: GOOGLE_SERVICE_ACCOUNT_JSON, GOOGLE_DOC_ID, and NOTES must all be set.")
    sys.exit(1)
 
# ── Auth ──────────────────────────────────────────────────────────────────────
 
credentials = service_account.Credentials.from_service_account_info(
    json.loads(sa_json),
    scopes=SCOPES,
)
service = build("docs", "v1", credentials=credentials)
 
# ── Get current end-of-document index ────────────────────────────────────────
 
doc     = service.documents().get(documentId=doc_id).execute()
content = doc.get("body", {}).get("content", [])
end_index = content[-1]["endIndex"] - 1  # insert before the final newline
 
# ── Build the insert request ──────────────────────────────────────────────────
 
# Prepend a separator so new entries are clearly delineated
text_to_insert = "\n\n" + "=" * 60 + "\n\n" + notes + "\n"
 
requests = [
    {
        "insertText": {
            "location": {"index": end_index},
            "text": text_to_insert,
        }
    }
]
 
# ── Apply ─────────────────────────────────────────────────────────────────────
 
result = service.documents().batchUpdate(
    documentId=doc_id,
    body={"requests": requests},
).execute()
 
print(f"Successfully appended release notes to Google Doc: {doc_id}")
print(f"Replies: {result.get('replies')}")
