const fs = require('fs');
const path = require('path');
const JSZip = require('jszip');

const root = path.resolve(__dirname, '..');
const input = path.join(root, '姘翠笅婊戠繑鏈鸿鏂囩粍浼氭眹鎶涓枃.pptx');

(async () => {
  const zip = await JSZip.loadAsync(fs.readFileSync(input));
  const slides = Object.keys(zip.files).filter(n => /^ppt\/slides\/slide\d+\.xml$/.test(n));
  slides.forEach(name => {
    let xml = zip.file(name).async ? null : null;
  });
  for (const name of slides) {
    let xml = await zip.file(name).async('string');
    if (!xml.includes('<p:transition')) {
      const transition = '<p:transition spd="slow"><p:fade/></p:transition>';
      xml = xml.replace('</p:sld>', `${transition}</p:sld>`);
      zip.file(name, xml);
    }
  }
  zip.file('ppt/presentation.xml', (await zip.file('ppt/presentation.xml').async('string')).replace('</p:presentation>', '<p:showPr><p:present><p:showAnimation/></p:present></p:showPr></p:presentation>'));
  fs.writeFileSync(input, await zip.generateAsync({ type: 'nodebuffer', compression: 'DEFLATE' }));
  console.log(`added fade transitions to ${slides.length} slides`);
})();
