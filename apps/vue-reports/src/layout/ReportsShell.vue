<template>
  <div :class="$style['shell']">
    <a :class="$style['skip']" href="#main">{{ copy.skipToContent }}</a>
    <header :class="$style['header']">
      <div :class="$style['brandBlock']">
        <p :class="$style['brand']">{{ copy.brand }}</p>
        <nav :class="$style['nav']" :aria-label="copy.primaryNavAria">
          <RouterLink
            to="/reports"
            :class="reportsNavClass"
          >
            {{ copy.reportsNav }}
          </RouterLink>
        </nav>
      </div>

      <div :class="$style['session']">
        <div v-if="session" :class="$style['who']" aria-live="polite">
          <span :class="$style['tenant']" :title="tenantTitle">{{ tenantLabel }}</span>
          <span :class="$style['sep']" aria-hidden="true">·</span>
          <span :class="$style['email']">{{ session.email }}</span>
          <span :class="$style['role']">{{ roleLabel }}</span>
        </div>
        <button type="button" :class="$style['signOut']" @click="signOut">
          {{ copy.signOut }}
        </button>
      </div>
    </header>

    <div id="main" :class="$style['content']" tabindex="-1">
      <RouterView />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, useCssModule, type ComputedRef, type Ref } from 'vue';
import { useRoute, useRouter, type RouteLocationNormalizedLoaded, type Router } from 'vue-router';
import { appRoleLabel } from '../auth/auth.mappers';
import type { ShellSession } from '../auth/auth.models';
import { onSessionCleared } from '../auth/session-events';
import { getValidShellSession } from '../auth/session';
import { tokenStorage } from '../auth/token-storage';
import { SHELL_MESSAGES, type ShellMessages, tenantIdTitle } from './shell.messages';

defineOptions({ name: 'ReportsShell' });

/**
 * Authenticated chrome host (KYC-080).
 * Login lives outside this shell; feature screens mount via RouterView.
 */
const copy: ShellMessages = SHELL_MESSAGES;
const styles: Record<string, string> = useCssModule();
const router: Router = useRouter();
const route: RouteLocationNormalizedLoaded = useRoute();
const session: Ref<ShellSession | null> = ref(getValidShellSession());
let unsubscribe: (() => void) | undefined;

const reportsNavClass: ComputedRef<string> = computed((): string => {
  const base: string = styles['navLink'] ?? '';
  const active: string = styles['navLinkActive'] ?? '';
  return route.path === '/reports' ? `${base} ${active}`.trim() : base;
});

const tenantLabel: ComputedRef<string> = computed((): string => {
  const current: ShellSession | null = session.value;
  return current?.tenantSlug?.trim() || current?.tenantId || '';
});

const tenantTitle: ComputedRef<string> = computed((): string => {
  const current: ShellSession | null = session.value;
  return current ? tenantIdTitle(current.tenantId) : '';
});

const roleLabel: ComputedRef<string> = computed((): string => {
  const current: ShellSession | null = session.value;
  return current ? appRoleLabel(current.role) : '';
});

onMounted((): void => {
  unsubscribe = onSessionCleared((): void => {
    session.value = null;
  });
});

onUnmounted((): void => {
  unsubscribe?.();
});

function signOut(): void {
  tokenStorage.clearSession();
  session.value = null;
  void router.replace('/login');
}
</script>

<style module>
.skip {
  position: absolute;
  left: var(--kyc-space-3);
  top: -100%;
  z-index: 2;
  padding: var(--kyc-space-2) var(--kyc-space-3);
  background: var(--kyc-color-surface-raised);
  color: var(--kyc-color-text);
  border-radius: var(--kyc-radius-sm);
  font-size: var(--kyc-text-sm);
  font-weight: 600;
}

.skip:focus,
.skip:focus-visible {
  top: var(--kyc-space-3);
}

.shell {
  box-sizing: border-box;
  min-height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--kyc-color-surface);
  color: var(--kyc-color-text);
  font-family: var(--kyc-font-sans);
}

.header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: var(--kyc-space-4);
  padding: var(--kyc-space-3) var(--kyc-page-gutter);
  border-bottom: 1px solid var(--kyc-color-border);
  background: var(--kyc-color-surface-raised);
}

.brandBlock {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--kyc-space-4);
}

.brand {
  margin: 0;
  font-size: var(--kyc-text-sm);
  font-weight: 600;
  color: var(--kyc-color-brand);
}

.nav {
  display: flex;
  align-items: center;
  gap: var(--kyc-space-3);
}

.navLink {
  font-size: var(--kyc-text-sm);
  font-weight: 600;
  color: var(--kyc-color-text-muted);
  text-decoration: none;
  padding: var(--kyc-space-1) var(--kyc-space-2);
  border-radius: var(--kyc-radius-sm);
}

.navLink:hover,
.navLink:focus-visible {
  color: var(--kyc-color-brand);
  text-decoration: underline;
}

.navLinkActive {
  color: var(--kyc-color-brand);
  background: var(--kyc-color-surface);
}

.session {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: flex-end;
  gap: var(--kyc-space-3);
}

.who {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--kyc-space-2);
  font-size: var(--kyc-text-sm);
  color: var(--kyc-color-text-muted);
}

.tenant {
  font-weight: 600;
  color: var(--kyc-color-text);
}

.email {
  font-family: var(--kyc-font-mono);
}

.role {
  padding: var(--kyc-space-1) var(--kyc-space-2);
  border-radius: var(--kyc-radius-sm);
  border: 1px solid var(--kyc-color-border);
  background: var(--kyc-color-surface);
  color: var(--kyc-color-text);
  font-size: var(--kyc-text-xs);
  font-weight: 600;
}

.sep {
  opacity: 0.5;
}

.signOut {
  min-height: 2.25rem;
  padding: var(--kyc-space-1) var(--kyc-space-3);
  border: 1px solid var(--kyc-color-border);
  border-radius: var(--kyc-radius-md);
  background: var(--kyc-color-surface-raised);
  color: var(--kyc-color-text);
  font: inherit;
  font-size: var(--kyc-text-sm);
  font-weight: 600;
  cursor: pointer;
}

.signOut:hover {
  border-color: var(--kyc-color-brand);
  color: var(--kyc-color-brand);
}

.signOut:focus-visible {
  outline: none;
  box-shadow: var(--kyc-focus-ring);
}

.content {
  flex: 1 1 auto;
}

.content:focus {
  outline: none;
}

@media (max-width: 640px) {
  .header {
    align-items: flex-start;
  }

  .session {
    width: 100%;
    justify-content: space-between;
  }
}
</style>
