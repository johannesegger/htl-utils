import { createRouter, createWebHistory } from 'vue-router'

import HomeView from './UserView.vue'
import AdminView from './AdminView.vue'

const routes = [
    { path: '/', component: HomeView },
    { path: '/admin', component: AdminView },
]

export const router = createRouter({
    history: createWebHistory(),
    routes
})
