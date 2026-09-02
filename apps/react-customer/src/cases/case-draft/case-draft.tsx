import {
  type MutableRefObject,
  type ReactElement,
  type SubmitEvent,
  useCallback,
  useEffect,
  useRef,
  useState,
} from 'react';
import { useParams } from 'react-router';
import { getCaseDetail, submitCase, updateDraftCase, uploadDocument } from '../cases-api';
import {
  emptyDraftForm,
  hasDraftFieldErrors,
  isCaseId,
  prependDocument,
  toCaseDetailLoadError,
  toDocumentUploadTransportError,
  toDraftActionError,
  validateDraftSave,
  validateDraftSubmit,
} from '../cases.mappers';
import { CASES_DRAFT_MESSAGES, type CasesDraftMessages } from '../cases.messages';
import {
  type CaseDocument,
  type CaseDraftDetail,
  type DraftFormFieldErrors,
  type DraftFormModel,
} from '../cases.models';
import { CaseDraftForm, CaseDraftReadonly } from './case-draft-form';
import { CaseDraftLoadError } from './case-draft-load-error';
import { CaseDraftLoading } from './case-draft-loading';

/**
 * Smart screen: customer draft editor, submit, and documents (KYC-073 / KYC-074).
 * Ownership is API JWT — client never sends tenant/user ids (ADR-007).
 */
export function CaseDraft(): ReactElement {
  const draftCopy: CasesDraftMessages = CASES_DRAFT_MESSAGES;
  const params: Readonly<Partial<Record<string, string>>> = useParams();
  const caseIdParam: string = params['caseId'] ?? '';
  const caseIdValid: boolean = isCaseId(caseIdParam);

  const loadSeq: MutableRefObject<number> = useRef(0);
  const actionLock: MutableRefObject<boolean> = useRef(false);
  const uploadLock: MutableRefObject<boolean> = useRef(false);
  const documentsRef: MutableRefObject<readonly CaseDocument[]> = useRef([]);

  const [detail, setDetail] = useState<CaseDraftDetail | null>(null);
  const [form, setForm] = useState<DraftFormModel>(emptyDraftForm());
  const [documents, setDocuments] = useState<readonly CaseDocument[]>([]);
  const [fieldErrors, setFieldErrors] = useState<DraftFormFieldErrors>({});
  const [touched, setTouched] = useState<boolean>(false);
  const [loading, setLoading] = useState<boolean>(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [saving, setSaving] = useState<boolean>(false);
  const [submitting, setSubmitting] = useState<boolean>(false);
  const [uploading, setUploading] = useState<boolean>(false);
  const [uploadError, setUploadError] = useState<string | null>(null);

  documentsRef.current = documents;

  function replaceDocuments(next: readonly CaseDocument[]): void {
    documentsRef.current = next;
    setDocuments(next);
  }

  const loadDetail = useCallback(async (caseId: string): Promise<void> => {
    const seq: number = loadSeq.current + 1;
    loadSeq.current = seq;
    setLoading(true);
    setLoadError(null);
    setActionError(null);
    setSuccessMessage(null);
    setUploadError(null);
    try {
      const loaded: CaseDraftDetail = await getCaseDetail(caseId);
      if (loadSeq.current !== seq) {
        return;
      }
      setDetail(loaded);
      setForm(loaded.form);
      replaceDocuments(loaded.documents);
      setFieldErrors({});
      setTouched(false);
      setLoading(false);
    } catch (err: unknown) {
      if (loadSeq.current !== seq) {
        return;
      }
      setDetail(null);
      setForm(emptyDraftForm());
      replaceDocuments([]);
      setLoading(false);
      setLoadError(toCaseDetailLoadError(err).message);
    }
  }, []);

  useEffect((): (() => void) | void => {
    if (!caseIdValid) {
      return;
    }
    void loadDetail(caseIdParam);
    return (): void => {
      loadSeq.current += 1;
    };
  }, [caseIdParam, caseIdValid, loadDetail]);

  if (!caseIdValid) {
    return (
      <section>
        <CaseDraftLoadError
          message={draftCopy.invalidCaseLink}
          showRetry={false}
          onRetry={(): void => undefined}
        />
      </section>
    );
  }

  function onFieldChange(field: keyof DraftFormModel, value: string): void {
    setForm((prev: DraftFormModel): DraftFormModel => ({ ...prev, [field]: value }));
    setSuccessMessage(null);
    setActionError(null);
  }

  async function onSave(event: SubmitEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    if (detail === null || !detail.canEdit || actionLock.current) {
      return;
    }

    setTouched(true);
    setActionError(null);
    setSuccessMessage(null);
    const errors: DraftFormFieldErrors = validateDraftSave(form);
    setFieldErrors(errors);
    if (hasDraftFieldErrors(errors)) {
      return;
    }

    actionLock.current = true;
    setSaving(true);
    try {
      const saved: CaseDraftDetail = await updateDraftCase(detail.id, form, {
        ...detail,
        documents: documentsRef.current,
      });
      const liveDocs: readonly CaseDocument[] = documentsRef.current;
      setDetail({ ...saved, documents: liveDocs });
      setForm(saved.form);
      setFieldErrors({});
      setSuccessMessage(draftCopy.saveSuccess);
    } catch (err: unknown) {
      setActionError(toDraftActionError(err, 'save').message);
    } finally {
      actionLock.current = false;
      setSaving(false);
    }
  }

  async function onSubmitCase(): Promise<void> {
    if (detail === null || !detail.canEdit || actionLock.current) {
      return;
    }

    setTouched(true);
    setActionError(null);
    setSuccessMessage(null);
    const errors: DraftFormFieldErrors = validateDraftSubmit(form);
    setFieldErrors(errors);
    if (hasDraftFieldErrors(errors)) {
      return;
    }

    actionLock.current = true;
    setSubmitting(true);
    let savedDraft: CaseDraftDetail | null = null;
    try {
      savedDraft = await updateDraftCase(detail.id, form, {
        ...detail,
        documents: documentsRef.current,
      });
      const submitted: CaseDraftDetail = await submitCase(savedDraft.id, {
        ...savedDraft,
        documents: documentsRef.current,
      });
      const liveDocs: readonly CaseDocument[] = documentsRef.current;
      setDetail({ ...submitted, documents: liveDocs });
      setForm(submitted.form);
      setFieldErrors({});
      setSuccessMessage(draftCopy.submitSuccess);
    } catch (err: unknown) {
      if (savedDraft !== null) {
        const liveDocs: readonly CaseDocument[] = documentsRef.current;
        setDetail({ ...savedDraft, documents: liveDocs });
        setForm(savedDraft.form);
        setActionError(toDraftActionError(err, 'submit').message);
      } else {
        setActionError(toDraftActionError(err, 'save').message);
      }
    } finally {
      actionLock.current = false;
      setSubmitting(false);
    }
  }

  async function onFileSelected(file: File): Promise<void> {
    if (
      detail === null ||
      !detail.canUpload ||
      uploadLock.current ||
      actionLock.current ||
      saving ||
      submitting
    ) {
      return;
    }

    uploadLock.current = true;
    setUploading(true);
    setUploadError(null);
    try {
      const uploaded: CaseDocument = await uploadDocument(detail.id, file);
      const next: readonly CaseDocument[] = prependDocument(
        documentsRef.current,
        uploaded,
      );
      replaceDocuments(next);
      setDetail((prev: CaseDraftDetail | null): CaseDraftDetail | null =>
        prev === null ? null : { ...prev, documents: next },
      );
    } catch (err: unknown) {
      setUploadError(toDocumentUploadTransportError(err).message);
    } finally {
      uploadLock.current = false;
      setUploading(false);
    }
  }

  const detailMatchesRoute: boolean =
    detail !== null && detail.id === caseIdParam;

  if (loadError !== null) {
    return (
      <section>
        <CaseDraftLoadError
          message={loadError}
          showRetry={caseIdValid}
          onRetry={(): void => {
            void loadDetail(caseIdParam);
          }}
        />
      </section>
    );
  }

  if (loading || !detailMatchesRoute || detail === null) {
    return (
      <section>
        <CaseDraftLoading />
      </section>
    );
  }

  if (!detail.canEdit) {
    return (
      <section>
        <CaseDraftReadonly
          detail={detail}
          successMessage={successMessage}
          documents={documents}
          uploading={uploading}
          uploadError={uploadError}
          uploadDisabled={false}
          onFileSelected={(file: File): void => {
            void onFileSelected(file);
          }}
        />
      </section>
    );
  }

  return (
    <section>
      <CaseDraftForm
        detail={detail}
        form={form}
        fieldErrors={fieldErrors}
        touched={touched}
        actionError={actionError}
        successMessage={successMessage}
        saving={saving}
        submitting={submitting}
        documents={documents}
        uploading={uploading}
        uploadError={uploadError}
        uploadDisabled={saving || submitting}
        onFieldChange={onFieldChange}
        onSave={(event: SubmitEvent<HTMLFormElement>): void => {
          void onSave(event);
        }}
        onSubmitCase={(): void => {
          void onSubmitCase();
        }}
        onFileSelected={(file: File): void => {
          void onFileSelected(file);
        }}
      />
    </section>
  );
}
