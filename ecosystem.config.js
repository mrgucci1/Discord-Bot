// PM2 configuration for the Discord Bot.
// Run `pm2 start ecosystem.config.js` after `dotnet publish -c Release -o publish`.
module.exports = {
  apps: [
    {
      name: 'discord-bot',
      script: 'dotnet',
      args: 'Discord-Bot.dll',
      cwd: './publish',
      interpreter: 'none',
      autorestart: true,
      restart_delay: 5000,
      max_restarts: 20,
      min_uptime: '30s',
      kill_timeout: 5000,
      watch: false,
      env: {
        DOTNET_CLI_TELEMETRY_OPTOUT: '1',
        DOTNET_NOLOGO: '1'
        // Set your token via `pm2 set` or a .env loader, or pass
        // `--update-env` after exporting DISCORD_BOT_TOKEN in your shell:
        //   export DISCORD_BOT_TOKEN=xxxxx
        //   pm2 restart discord-bot --update-env
      },
      out_file: './logs/out.log',
      error_file: './logs/err.log',
      merge_logs: true,
      time: true
    }
  ]
};
