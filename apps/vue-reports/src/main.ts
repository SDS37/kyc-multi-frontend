import { createApp, type App as VueApp } from 'vue';
import App from './App.vue';
import { appRouter } from './app-router';
import './styles.css';

const app: VueApp = createApp(App);
app.use(appRouter);
app.mount('#app');
