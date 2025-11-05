window.quizCodeEditors = {};

window.initMonaco = function (containerId, language, initialCode) {
    // Monaco via CDN loader initialized in the page (see partial below)
    require(['vs/editor/editor.main'], function () {
        const editor = monaco.editor.create(document.getElementById(containerId), {
            value: initialCode || '',
            language: language || 'python',
            automaticLayout: true,
            minimap: { enabled: false },
            theme: 'vs-dark'
        });
        window.quizCodeEditors[containerId] = editor;
    });
};

window.getCode = function (containerId) {
    return window.quizCodeEditors[containerId]?.getValue() || '';
};
