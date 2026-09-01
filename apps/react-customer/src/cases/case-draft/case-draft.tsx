import {
  type FormEvent,
  type ReactElement,
  type RefObject,
  useCallback,
  useEffect,
  useRef,
  useState,
} from 'react';
import { useParams } from 'react-router';
import { getCaseDetail, submitCase, updateDraftCase } from '../cases-api';
import {
  emptyDraftForm,
  hasDraftFieldErrors,
  isCaseId,
  toCaseDetailLoadError,
  toDraftActionError,
  validateDraftSave,
  validateDraftSubmit,
} from '../cases.mappers';
import { CASES_DRAFT_MESSAGES, type CasesDraftMessages } from '../cases.messages';
import {
  type CaseDraftDetail,
  type DraftFormFieldErrors,
  type DraftFormModel,
} from '../cases.models';
import { CaseDraftForm, CaseDraftReadonly } from './case-draft-form';
import { CaseDraftLoadError } from './case-draft-load-error';
import { CaseDraftLoading } from './case-draft-loading';

/**
 * Smart screen: customer draft editor + submit (KYC-073).
 * Ownership is API JWT — client never sends tenant/user ids (ADR-007).
 */
export function CaseDraft(): ReactElement {
  const draftCopy: CasesDraftMessages = CASES_DRAFT_MESSAGES;
  const params: Readonly<Partial<Record<string, string>>> = useParams();
  const caseIdParam: string = params['caseId'] ?? '';
  const caseIdValid: boolean = isCaseId(caseIdParam);

  const loadSeq: RefObject<number> = useRef(0);
  const actionLock: RefObject<boolean> = useRef(false);

  const [detail, setDetail] = useState<CaseDraftDetail | null>(null);
  const [form, setForm] = useState<DraftFormModel>(emptyDraftForm());
  const [fieldErrors, setFieldErrors] = useState<DraftFormFieldErrors>({});
  const [touched, setTouched] = useState<boolean>(false);
  const [loading, setLoading] = useState<boolean>(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [saving, setSaving] = useState<boolean>(false);
  const [submitting, setSubmitting] = useState<boolean>(false);

  const loadDetail = useCallback(async (caseId: string): Promise<void> => {
    const seq: number = loadSeq.current + 1;
    loadSeq.current = seq;
    setLoading(true);
    setLoadError(null);
    setActionError(null);
    setSuccessMessage(null);
    try {
      const loaded: CaseDraftDetail = await getCaseDetail(caseId);
      if (loadSeq.current !== seq) {
        return;
      }
      setDetail(loaded);
      setForm(loaded.form);
      setFieldErrors({});
      setTouched(false);
      setLoading(false);
    } catch (err: unknown) {
      if (loadSeq.current !== seq) {
        return;
      }
      setDetail(null);
      setForm(emptyDraftForm());
      setLoading(false);
      setLoadError(toCaseDetailLoadError(err).message);
    }
  }, []);

  useEffect((): void => {
    if (!caseIdValid) {
      return;
    }
    void loadDetail(caseIdParam);
  }, [caseIdParam, caseIdValid, loadDetail]);

  if (!caseIdValid) {
    return (
      <main>
        <CaseDraftLoadError
          message={draftCopy.invalidCaseLink}
          showRetry={false}
          onRetry={(): void => undefined}
        />
      </main>
    );
  }

  function onFieldChange(field: keyof DraftFormModel, value: string): void {
    setForm((prev: DraftFormModel): DraftFormModel => ({ ...prev, [field]: value }));
    setSuccessMessage(null);
    setActionError(null);
  }

  async function onSave(event: FormEvent<HTMLFormElement>): Promise<void> {
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
      const saved: CaseDraftDetail = await updateDraftCase(detail.id, form);
      setDetail(saved);
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
      // Persist FormData first so submit validates against the server copy.
      savedDraft = await updateDraftCase(detail.id, form);
      const submitted: CaseDraftDetail = await submitCase(savedDraft.id, savedDraft);
      setDetail(submitted);
      setForm(submitted.form);
      setFieldErrors({});
      setSuccessMessage(draftCopy.submitSuccess);
    } catch (err: unknown) {
      if (savedDraft !== null) {
        setDetail(savedDraft);
        setForm(savedDraft.form);
      }
      setActionError(toDraftActionError(err, 'submit').message);
    } finally {
      actionLock.current = false;
      setSubmitting(false);
    }
  }

  const detailMatchesRoute: boolean =
    detail !== null && detail.id === caseIdParam;

  if (loadError !== null) {
    return (
      <main>
        <CaseDraftLoadError
          message={loadError}
          showRetry={caseIdValid}
          onRetry={(): void => {
            void loadDetail(caseIdParam);
          }}
        />
      </main>
    );
  }

  if (loading || !detailMatchesRoute || detail === null) {
    return (
      <main>
        <CaseDraftLoading />
      </main>
    );
  }

  if (!detail.canEdit) {
    return (
      <main>
        <CaseDraftReadonly detail={detail} successMessage={successMessage} />
      </main>
    );
  }

  return (
    <main>
      <CaseDraftForm
        detail={detail}
        form={form}
        fieldErrors={fieldErrors}
        touched={touched}
        actionError={actionError}
        successMessage={successMessage}
        saving={saving}
        submitting={submitting}
        onFieldChange={onFieldChange}
        onSave={(event: FormEvent<HTMLFormElement>): void => {
          void onSave(event);
        }}
        onSubmitCase={(): void => {
          void onSubmitCase();
        }}
      />
    </main>
  );
}
