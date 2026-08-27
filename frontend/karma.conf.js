/**
 * Configuração do Karma.
 *
 * Só existe por causa do launcher `ChromeHeadlessNoSandbox`, necessário para
 * rodar os testes dentro de contêiner (CI, Docker), onde o Chrome roda como
 * root e recusa o sandbox:
 *
 *     npm run test:ci
 *
 * Fora do contêiner, `npm test` continua usando o Chrome normal.
 */
module.exports = (config) => {
  config.set({
    frameworks: ['jasmine'],
    plugins: [
      require('karma-jasmine'),
      require('karma-chrome-launcher'),
      require('karma-jasmine-html-reporter'),
      require('karma-coverage'),
    ],
    reporters: ['progress', 'kjhtml'],
    browsers: ['ChromeHeadless'],
    customLaunchers: {
      ChromeHeadlessNoSandbox: {
        base: 'ChromeHeadless',
        flags: ['--no-sandbox', '--disable-gpu', '--disable-dev-shm-usage'],
      },
    },
    restartOnFileChange: true,
  });
};
