"""Extract bilingual labels into embedded .NET resources; keep source fallback labels."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET
root = Path(__file__).resolve().parent.parent
source = (root / 'src/KubSMTP.App/MainWindow.cs').read_text(encoding='utf-8-sig')
pairs = re.findall(r'T\("([^"\n]*)",\s*"([^"\n]*)"\)', source)
def csharp_text(value):
    escapes = {'\\': '\\', '"': '"', 'r': '\r', 'n': '\n', 't': '\t'}
    return re.sub(r'\\([\\"rnt])', lambda match: escapes[match[1]], value)
pairs = [tuple(csharp_text(value) for value in pair) for pair in pairs]
for lang, index in [('', 1), ('.de', 0)]:
    res = ET.Element('root')
    for name, value in [('resmimetype','text/microsoft-resx'),('version','2.0'),('reader','System.Resources.ResXResourceReader, System.Windows.Forms'),('writer','System.Resources.ResXResourceWriter, System.Windows.Forms')]:
        ET.SubElement(ET.SubElement(res,'resheader',name=name),'value').text = value
    for english, pair in sorted({pair[1]: pair for pair in pairs}.items()):
        data = ET.SubElement(res, 'data', name=english)
        data.set('{http://www.w3.org/XML/1998/namespace}space','preserve')
        ET.SubElement(data,'value').text = pair[index]
    ET.indent(res)
    target = root / f'src/KubSMTP.App/Resources/Strings{lang}.resx'
    target.parent.mkdir(parents=True,exist_ok=True)
    ET.ElementTree(res).write(target,encoding='utf-8',xml_declaration=True)
print(f'{len(pairs)} localized label calls extracted')
