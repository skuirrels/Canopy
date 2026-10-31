import test from 'node:test';
import assert from 'node:assert/strict';
import { parseCheck, checkCommand, commandDraft } from '../src/lib/factory-setup.ts';

test('direct checks preserve quoted, empty and escaped arguments without shell execution',()=>{
 assert.deepEqual(parseCheck('runner test "a b" \'\' one\\ two'),['runner','test','a b','','one two']);
 assert.deepEqual(parseCheck('"C:\\Program Files\\runner.exe" test'),['C:\\Program Files\\runner.exe','test']);
 assert.deepEqual(parseCheck('runner \'$literal\''),['runner','$literal']);
 for(const line of ['runner test && runner build','runner | tee output','runner > output','runner $HOME','runner `date`','runner "missing']) assert.throws(()=>parseCheck(line));
});
test('real command settings round-trip without losing literal arguments or environment references',()=>{
 const command={executable:'/tools/my runner',arguments:['test',"it's",'a b','', '$input','back\\slash','say "yes"'],workingDirectory:'src',timeoutSeconds:43,environmentReferences:{TOKEN:'CALLER_TOKEN'}};
 assert.deepEqual(checkCommand(commandDraft(command)),command);
});
test('missing checks, directories and valid time bounds stay visible rather than receiving defaults',()=>{
 for(const change of [{line:''},{directory:''},{timeout:''},{timeout:'0'},{timeout:'1.5'},{timeout:'Infinity'}]) assert.throws(()=>checkCommand({line:'runner test',directory:'.',timeout:'43',...change}));
 assert.equal(checkCommand({line:'runner test',directory:'.',timeout:'43'}).timeoutSeconds,43);
});
