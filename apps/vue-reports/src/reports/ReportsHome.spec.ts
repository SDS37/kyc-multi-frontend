import { describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import ReportsHome from './ReportsHome.vue';
import { REPORTS_HOME_MESSAGES } from './reports.messages';

describe('ReportsHome', () => {
  it('renders the reports heading from the message catalog', (): void => {
    const wrapper = mount(ReportsHome);
    expect(wrapper.get('#reports-heading').text()).toBe(REPORTS_HOME_MESSAGES.pageTitle);
    expect(wrapper.text()).toContain(REPORTS_HOME_MESSAGES.pendingHint);
  });
});
