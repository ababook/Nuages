const implants = require('./implants/implants.service.js');
const jobs = require('./jobs/jobs.service.js');
const implantRegister = require('./implant-register/implant-register.service.js');
const implantHeartbeat = require('./implant-heartbeat/implant-heartbeat.service.js');
const implantJobresult = require('./implant-jobresult/implant-jobresult.service.js');
const fsFiles = require('./fs.files/fs.files.service.js');
const fsChunks = require('./fs.chunks/fs.chunks.service.js');
const fs = require('./fs/fs.service.js');
const implantChunks = require('./implant-chunks/implant-chunks.service.js');
const users = require('./users/users.service.js');
const modules = require('./modules/modules.service.js');
const modulesRun = require('./modules-run/modules-run.service.js');
const modulesLoad = require('./modules-load/modules-load.service.js');
const handlers = require('./handlers/handlers.service.js');
const handlersLoad = require('./handlers-load/handlers-load.service.js');
const listeners = require('./listeners/listeners.service.js');
const listenersStartstop = require('./listeners-startstop/listeners-startstop.service.js');
// eslint-disable-next-line no-unused-vars
module.exports = function (app) {
  app.configure(implants);
  app.configure(jobs);
  app.configure(implantRegister);
  app.configure(implantHeartbeat);
  app.configure(implantJobresult);
  app.configure(fsFiles);
  app.configure(fsChunks);
  app.configure(fs);
  app.configure(implantChunks);
  app.configure(users);
  app.configure(modules);
  app.configure(modulesRun);
  app.configure(modulesLoad);
  app.configure(handlers);
  app.configure(handlersLoad);
  app.configure(listeners);
  app.configure(listenersStartstop);
};
