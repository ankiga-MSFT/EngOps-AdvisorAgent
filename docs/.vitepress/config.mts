import { defineConfig } from 'vitepress'

export default defineConfig({
  title: 'Advisor Agent',
  description: 'Architecture documentation for the Azure Advisor Agent backend',
  themeConfig: {
    logo: '/logo.svg',
    nav: [
      { text: 'Home', link: '/' },
      { text: 'Architecture', link: '/architecture/' },
      { text: 'Skills', link: '/skills/' },
      { text: 'API', link: '/api/' },
    ],
    sidebar: [
      {
        text: 'Getting Started',
        items: [
          { text: 'Overview', link: '/architecture/' },
          { text: 'Project Structure', link: '/architecture/project-structure' },
        ],
      },
      {
        text: 'Core Architecture',
        items: [
          { text: 'End-to-End Flow', link: '/architecture/end-to-end-flow' },
          { text: 'Orchestration Pipeline', link: '/architecture/orchestration' },
          { text: 'Task Planning & Execution', link: '/architecture/task-planning' },
          { text: 'Durable Functions', link: '/architecture/durable-functions' },
        ],
      },
      {
        text: 'Skills & Tools',
        items: [
          { text: 'Skill System Overview', link: '/skills/' },
          { text: 'Retirement Skill', link: '/skills/retirement' },
          { text: 'Resiliency Skill', link: '/skills/resiliency' },
          { text: 'Cost Optimization Skill', link: '/skills/cost-optimization' },
          { text: 'Outage Remediation Skill', link: '/skills/outage-remediation' },
          { text: 'Architecture Skill', link: '/skills/architecture' },
          { text: 'Tool Reference', link: '/skills/tools' },
        ],
      },
      {
        text: 'Data & Conversation',
        items: [
          { text: 'Domain Models', link: '/models/' },
          { text: 'Conversation Management', link: '/models/conversation' },
          { text: 'Azure Context Resolution', link: '/models/azure-context' },
        ],
      },
      {
        text: 'API & Configuration',
        items: [
          { text: 'HTTP API Reference', link: '/api/' },
          { text: 'Configuration', link: '/api/configuration' },
          { text: 'Deployment', link: '/api/deployment' },
        ],
      },
    ],
    socialLinks: [
      { icon: 'github', link: '#' },
    ],
    search: {
      provider: 'local',
    },
    outline: {
      level: [2, 3],
    },
  },
})
