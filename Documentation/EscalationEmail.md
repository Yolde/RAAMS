# Escalation email API

The API stores an uploaded email file and its evidence metadata in SQL Server.
It does not send the email or verify its recipients or contents.

- `POST /api/assessments/{id}/escalation-email`: multipart form with `file`
  and optional `notes`. The accepted content types are `application/pdf`
  (PDF), `message/rfc822` (EML), `application/vnd.ms-outlook` (MSG),
  `application/vnd.openxmlformats-officedocument.wordprocessingml.document`
  (DOCX), and `application/msword` (DOC). The file must contain between 1
  byte and 20 MiB. The content type is supplied by the client and is not
  verified against the file bytes. The
  authenticated caller must be an Auditee or GRA; the assessment must meet
  the escalation criteria and its actions must be closed.
- `GET /api/assessments/{id}/escalation-emails`: lists active assessment-only
  evidence metadata.
- `GET /api/assessments/{id}/escalation-emails/{emailId}/file`: downloads the
  stored file. The endpoint returns 404 if the evidence is deleted, belongs
  to another assessment, or has no stored file.

Listing and downloading require Assessments module authorization; they are
not restricted by assessment record ownership. Upload notes are included in
the audit reason, which has a 500-character limit including the filename and
other generated text. Email content is not included in the audit record.

Existing metadata-only evidence has no file to download and remains visible
in the list. Clients that previously posted JSON metadata must switch to
multipart form data. Deploy the migrations through
`AddLodReportableAndAssessmentEvidence` and `AddEvidenceContent` before
using this functionality. The `AddEvidenceContent` rollback refuses to
remove stored files.
