#!/usr/bin/env node
'use strict';

const { spawnSync } = require('child_process');
const path = require('path');

const dll = path.join(__dirname, '..', 'dist', 'aria', 'aria.dll');
const result = spawnSync('dotnet', [dll, ...process.argv.slice(2)], { stdio: 'inherit' });

if (result.error) {
    const msg = result.error.code === 'ENOENT'
        ? 'aria: .NET runtime not found. Install .NET 9 SDK or Runtime from https://dot.net\n'
        : `aria: failed to launch: ${result.error.message}\n`;
    process.stderr.write(msg);
    process.exit(1);
}

process.exit(result.status ?? 1);
