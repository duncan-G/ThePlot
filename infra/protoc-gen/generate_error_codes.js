#!/usr/bin/env node

const fs = require('fs');
const path = require('path');

async function findErrorCodeFiles(rootDir) {
  const entries = await fs.promises.readdir(rootDir, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const fullPath = path.join(rootDir, entry.name);
    if (entry.isDirectory()) {
      const nested = await findErrorCodeFiles(fullPath);
      files.push(...nested);
    } else if (entry.isFile() && /.*ErrorCodes\.cs$/i.test(entry.name)) {
      files.push(fullPath);
    }
  }
  return files;
}

function parseCSharpConstants(content) {
  const results = {};
  const constRegex = /public\s+const\s+string\s+(\w+)\s*=\s*"(\d+)"\s*;/g;
  let match;
  while ((match = constRegex.exec(content)) !== null) {
    const name = match[1];
    const value = match[2];
    results[name] = value;
  }
  return results;
}

function generateTsContent(constants) {
  const lines = [];
  lines.push('// This file is auto-generated. Do not edit manually.');
  lines.push('// To regenerate, run: node infra/protoc-gen/generate_error_codes.js');
  lines.push('');
  lines.push('export const ErrorCodes = {');
  const sortedKeys = Object.keys(constants).sort((a, b) => {
    const aVal = Number(constants[a]);
    const bVal = Number(constants[b]);
    if (aVal === bVal) {
      return a.localeCompare(b);
    }
    return aVal - bVal;
  });
  for (const key of sortedKeys) {
    lines.push(`    ${key}: "${constants[key]}",`);
  }
  lines.push('} as const');
  lines.push('');
  lines.push('export type KnownErrorCode = (typeof ErrorCodes)[keyof typeof ErrorCodes]');
  return lines.join('\n');
}

async function main() {
  const repoRoot = path.resolve(__dirname, '..', '..');
  // Adjust csRoot to match your C# ErrorCodes location (e.g. libraries/Exceptions or src/**/Exceptions)
  const csRoot = path.join(repoRoot, 'libs', 'Exceptions');
  const outFile = path.join(
    repoRoot,
    'client',
    'src',
    'lib',
    'services',
    'error-codes.ts'
  );

  const files = await findErrorCodeFiles(csRoot);
  if (files.length === 0) {
    console.warn(`No *ErrorCodes.cs files found in ${csRoot}. Skipping generation.`);
    return;
  }

  const all = {};
  for (const file of files) {
    const content = await fs.promises.readFile(file, 'utf8');
    const parsed = parseCSharpConstants(content);
    for (const [k, v] of Object.entries(parsed)) {
      all[k] = v;
    }
  }

  await fs.promises.mkdir(path.dirname(outFile), { recursive: true });
  const tsContent = generateTsContent(all);
  await fs.promises.writeFile(outFile, tsContent, 'utf8');
  console.log(`Generated ${outFile}`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
