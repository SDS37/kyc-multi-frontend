import type { ChangeEvent, ReactElement, RefObject } from 'react';
import { useRef } from 'react';
import {
  CASES_DRAFT_MESSAGES,
  type CasesDraftMessages,
} from '../cases.messages';
import type { CaseDocument } from '../cases.models';
import styles from './case-documents-pane.module.css';

const DOCUMENT_ACCEPT: string =
  '.pdf,.png,.jpg,.jpeg,application/pdf,image/png,image/jpeg';

export type CaseDocumentsPaneProps = {
  readonly documents: readonly CaseDocument[];
  readonly canUpload: boolean;
  readonly uploading: boolean;
  readonly uploadError: string | null;
  readonly onFileSelected: (file: File) => void;
};

/**
 * Presentational documents list + optional upload control (KYC-074).
 * Props/callbacks only — no GraphQL / REST.
 */
export function CaseDocumentsPane(props: CaseDocumentsPaneProps): ReactElement {
  const copy: CasesDraftMessages = CASES_DRAFT_MESSAGES;
  const inputRef: RefObject<HTMLInputElement | null> = useRef<HTMLInputElement | null>(
    null,
  );

  function onInputChange(event: ChangeEvent<HTMLInputElement>): void {
    const file: File | undefined = event.target.files?.[0];
    event.target.value = '';
    if (!file || props.uploading || !props.canUpload) {
      return;
    }
    props.onFileSelected(file);
  }

  return (
    <section className={styles['pane']} aria-labelledby="docs-heading">
      <h2 id="docs-heading" className={styles['title']}>
        {copy.docsHeading}
      </h2>

      {props.canUpload ? (
        <div className={styles['uploadRow']}>
          <input
            ref={inputRef}
            className={styles['fileInput']}
            type="file"
            accept={DOCUMENT_ACCEPT}
            disabled={props.uploading}
            aria-label={copy.docsUploadLabel}
            aria-describedby="docs-accept-hint"
            onChange={onInputChange}
          />
          <p id="docs-accept-hint" className={styles['hint']}>
            {copy.docsAcceptHint}
          </p>
          {props.uploading ? (
            <span className={styles['uploading']} role="status" aria-live="polite">
              {copy.docsUploading}
            </span>
          ) : null}
        </div>
      ) : null}

      {props.uploadError !== null ? (
        <p className={styles['alert']} role="alert">
          {props.uploadError}
        </p>
      ) : null}

      {props.documents.length === 0 ? (
        <p className={styles['muted']} role="status">
          {copy.docsEmpty}
        </p>
      ) : (
        <ul className={styles['list']}>
          {props.documents.map(
            (doc: CaseDocument): ReactElement => (
              <li key={doc.id} className={styles['item']}>
                <span className={styles['name']}>{doc.fileName}</span>
                <span className={styles['meta']}>
                  {doc.sizeLabel} · {doc.contentType} · {doc.uploadedAtLabel}
                </span>
              </li>
            ),
          )}
        </ul>
      )}
    </section>
  );
}
