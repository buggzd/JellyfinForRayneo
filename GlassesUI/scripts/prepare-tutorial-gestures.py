"""Adapt the user-provided thumb-gestures-svg.zip without executing its scripts."""

import argparse
from copy import deepcopy
from pathlib import Path
from zipfile import ZipFile
import xml.etree.ElementTree as ET

SVG = '{http://www.w3.org/2000/svg}'
ET.register_namespace('', SVG[1:-1])
GESTURES = ('swipe-right', 'swipe-down', 'swipe-left', 'swipe-up', 'single-tap', 'double-tap')


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


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('archive', type=Path)
    args = parser.parse_args()
    prepare(args.archive, Path(__file__).resolve().parents[1] / 'public/assets/tutorial')
    print('Prepared six hand gestures and a full-figure single tap, each with a still fallback.')
