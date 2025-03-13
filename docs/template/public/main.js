import WorkflowContainer from "./workflow.js"

export default {
    defaultTheme: 'auto',
    iconLinks: [{
        icon: 'github',
        href: 'https://github.com/bonsai-rx/sgen',
        title: 'GitHub'
    }],
    start: () => {
        WorkflowContainer.init();
    }
}
