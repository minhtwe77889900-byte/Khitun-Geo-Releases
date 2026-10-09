const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const project = fs.readFileSync(path.join(root, 'src/KhitunGeo/KhitunGeo.csproj'), 'utf8');
const program = fs.readFileSync(path.join(root, 'src/KhitunGeo/Program.cs'), 'utf8');

assert.doesNotMatch(project, /Microsoft\.Web\.WebView2/i, 'the application must not reference the WebView2 package');
assert.match(program, /Application\.Run\(new Native\.NativeWorkspaceForm\(\)\)/, 'normal startup must use the native workspace');
assert.doesNotMatch(program, /--native-preview|MainForm/, 'native workspace must not be an opt-in preview');

const source = path.join(root, 'src/KhitunGeo');
const forbidden = /Microsoft\.Web\.WebView2|CoreWebView2|\bWebView2\b/;
const scan = (directory) => {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const file = path.join(directory, entry.name);
    if (entry.isDirectory()) scan(file);
    else if (entry.isFile() && entry.name.endsWith('.cs')) {
      assert.doesNotMatch(fs.readFileSync(file, 'utf8'), forbidden, `WebView2 reference remains in ${path.relative(root, file)}`);
    }
  }
};
scan(source);

assert.match(project, /Exclude="[^"]*wwwroot\\index\.html;wwwroot\\workspace-ui\.js/, 'the legacy browser UI must not ship');
for (const obsolete of ['MainForm.cs', 'MainForm.Drawing.cs', 'MainForm.Updates.cs', 'BackupService.cs', 'ProjectStore.cs']) {
  assert.equal(fs.existsSync(path.join(source, obsolete)), false, `${obsolete} must not be part of the production application`);
}
const paths = fs.readFileSync(path.join(source, 'AppPaths.cs'), 'utf8');
assert.doesNotMatch(paths, /WebViewDataDirectory|BackupsDirectory|SessionMarkerFile/, 'browser data and hidden backup paths must be removed');

console.log('PASS production startup and packaged sources do not depend on WebView2');
