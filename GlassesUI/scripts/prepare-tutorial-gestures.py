"""Adapt the user-provided thumb-gestures-svg.zip without executing its scripts."""

import argparse
from copy import deepcopy
from pathlib import Path
from zipfile import ZipFile
import xml.etree.ElementTree as ET

SVG = '{http://www.w3.org/2000/svg}'
ET.register_namespace('', SVG[1:-1])
GESTURES = ('swipe-right', 'swipe-down', 'swipe-left', 'swipe-up', 'single-tap', 'double-tap')
# Bake the gallery palette into static vectors; an <img> cannot inherit page
# colors, and a runtime hue filter would defeat simpleUI's rendering budget.
SIMPLE_PALETTE = {
    '#1b6ef2': '#859477', '#1d70f3': '#89987b',
    '#125cd4': '#586a4e', '#155fd7': '#617456', '#165fd6': '#637659',
    '#1260d9': '#5b6e51', '#1460d9': '#65765b', '#1466e3': '#718366',
    '#176be9': '#86967a', '#1666dc': '#7d8f70', '#1669df': '#7d8f70',
    '#2475f4': '#adbba0', '#2b7cf5': '#bcc8b0', '#2f7df5': '#c8d2bc',
    '#2d7ef6': '#c1cdb4', '#2b79f3': '#b9c6ac', '#2675f1': '#aebb9f',
    '#2979f2': '#b6c3a9', '#a5dfff': '#cfdbc0', '#bfebff': '#dce4cd',
    '#c2ebff': '#dce4cd', '#e8f6ff': '#f0eee6',
}


def parse_vector(archive, name):
    root = ET.fromstring(archive.read('thumb-gestures/' + name + '.svg'))
    for element in root.iter():
        if element.tag.split('}')[-1] in ('script', 'foreignObject', 'image'):
            raise ValueError('Only self-contained vector artwork is supported.')
        if any(key.startswith('on') or key.endswith('href') for key in element.attrib):
            raise ValueError('External references and event handlers are not supported.')
    return root


def write_svg(root, path):
    ET.ElementTree(root).write(path, encoding='utf-8', xml_declaration=True)


def write_gesture(root, output, name, contact_frame):
    write_svg(root, output / (name + '.svg'))
    for parent in root.iter():
        for child in list(parent):
            if child.tag != SVG + 'animate':
                continue
            values = child.get('values', '').split(';')
            # A held contact pose makes the reduced-motion fallback useful.
            if len(values) > contact_frame:
                parent.set(child.get('attributeName'), values[contact_frame])
            parent.remove(child)
    write_svg(root, output / (name + '-still.svg'))


def prepare(source, output):
    output.mkdir(parents=True, exist_ok=True)
    with ZipFile(source) as archive:
        for gesture in GESTURES:
            original = parse_vector(archive, 'thumb-' + gesture)
            root = ET.Element(SVG + 'svg', {
                'width': '280', 'height': '440', 'viewBox': '-18 -12 140 220',
                'role': 'img', 'aria-labelledby': 'title',
            })
            ET.SubElement(root, SVG + 'title', {'id': 'title'}).text = original.find(SVG + 'title').text
            root.append(deepcopy(original.find(SVG + 'defs')))
            hand = deepcopy(next(element for element in original.iter() if element.get('id') == 'phone-and-hand'))
            del hand.attrib['transform']
            # Finish the cropped wrist with a sleeve, behind the animated palm.
            root.append(ET.Element(SVG + 'path', {
                'd': 'M58 179 Q74 174 97 180 L103 208 H51Z', 'fill': '#1b6ef2',
            }))
            root.append(hand)
            write_gesture(root, output, gesture, 67 if gesture == 'double-tap' else 57)
        person = parse_vector(archive, 'thumb-single-tap')
        person.attrib.update({'viewBox': '160 0 575 576', 'width': '575', 'height': '576'})
        for parent in person.iter():
            for child in list(parent):
                if child.get('id') == 'background':
                    parent.remove(child)
        write_gesture(person, output, 'single-tap-full', 57)


def prepare_simple_gestures(output):
    target = output / 'simpleUI'
    target.mkdir(parents=True, exist_ok=True)
    for name in (*GESTURES, 'single-tap-full'):
        root = ET.parse(output / (name + '-still.svg')).getroot()
        for element in root.iter():
            if element.tag.split('}')[-1] in ('animate', 'animateTransform', 'animateMotion', 'set'):
                raise ValueError('simpleUI artwork must remain static.')
            for attribute, value in list(element.attrib.items()):
                if value in SIMPLE_PALETTE:
                    element.set(attribute, SIMPLE_PALETTE[value])
        description = root.find(SVG + 'desc')
        if description is not None:
            description.text = '苔绿与暖灰配色的静态遥控示意，纯矢量，无滤镜或循环动画。'
        write_svg(root, target / (name + '-still.svg'))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('archive', type=Path, nargs='?')
    parser.add_argument('--simple-ui-only', action='store_true',
                        help='Regenerate the simpleUI palette from the committed still vectors.')
    args = parser.parse_args()
    output = Path(__file__).resolve().parents[1] / 'public/assets/tutorial'
    if not args.simple_ui_only:
        if args.archive is None:
            parser.error('Provide the source archive, or use --simple-ui-only.')
        prepare(args.archive, output)
    prepare_simple_gestures(output)
    print('Prepared tutorial artwork, including seven static simpleUI vectors.')
