import json
import colorsys

def load_and_parse_json(file_path):
    try:
        with open(file_path, 'r') as file:
            json_data = file.read()
        data = json.loads(json_data)
        return data
    except FileNotFoundError:
        print(f"Error: File '{file_path}' not found.")
    except json.JSONDecodeError as e:
        print(f"Error parsing JSON: {e}")
    except Exception as e:
        print(f"An unexpected error occurred: {e}")
    return None

def generate_colors(num_colors):
    colors = []
    for i in range(num_colors):
        hue = i / num_colors
        saturation = 0.7  # High saturation for section colors
        value = 0.9
        rgb = colorsys.hsv_to_rgb(hue, saturation, value)
        hex_color = '#{:02x}{:02x}{:02x}'.format(int(rgb[0]*255), int(rgb[1]*255), int(rgb[2]*255))
        colors.append(hex_color)
    return colors

def get_color(section_color, level):
    # Desaturate the color based on the level
    rgb = tuple(int(section_color.lstrip('#')[i:i+2], 16) for i in (0, 2, 4))
    hsv = colorsys.rgb_to_hsv(rgb[0]/255, rgb[1]/255, rgb[2]/255)

    saturation_factors = {
        "section": 1.0,
        "2digit": 0.8,
        "4digit": 0.6,
        "6digit": 0.4,
    }

    new_saturation = hsv[1] * saturation_factors.get(level, 0.2)
    new_rgb = colorsys.hsv_to_rgb(hsv[0], new_saturation, hsv[2])
    return '#{:02x}{:02x}{:02x}'.format(int(new_rgb[0]*255), int(new_rgb[1]*255), int(new_rgb[2]*255))

def get_node_size(level):
    sizes = {
        "section": 4.0,
        "2digit": 3.5,
        "3digit": 3.0,
        "4digit": 2.5,
        "5digit": 2.0,
        "6digit": 1.5,
    }
    return sizes.get(level, 1.0)  # Default to 1.0 for unknown levels

def create_clustered_dot_graph(json_data):
    dot_output = ["digraph G {"]
    #dot_output.append('    node [shape=box, style="rounded,filled", color="#4285F4", fillcolor="#E1E1E1"];')
    dot_output.append('    node [shape=box, style="rounded,filled"];')
    dot_output.append('    edge [color="#4285F4"];')

    # Generate distinct colors for sections
    sections = set(item['code'][0] for item in json_data['data'] if item['code'])
    section_colors = dict(zip(sections, generate_colors(len(sections))))

    # Create clusters based on the first character of the 'code' field
    clusters = {}
    for item in json_data['data']:
        cluster_key = item['code'][0] if item['code'] else 'misc'
        if cluster_key not in clusters:
            clusters[cluster_key] = []
        clusters[cluster_key].append(item)

    for cluster_key, items in clusters.items():
        dot_output.append(f'    subgraph cluster_{cluster_key} {{')
        dot_output.append(f'        label="Cluster {cluster_key}";')
        for item in items:
            item_id = item['id']
            name = item['name_short_en']
            code = item['code']
            level = item['level']
            # Calculate node size based on level
            size = get_node_size(level)

            # Get color based on section and level
            section = code[0] if code else 'misc'
            color = get_color(section_colors.get(section, '#CCCCCC'), level)

            # Add node with more details, adjusted size, and color
            node_label = f"{name}"#\\nID: {item_id}\\nCode: {code}\\nLevel: {level}"
            dot_output.append(f'    "{item_id}" [label="{node_label}", width={size}, height={size}, '
                            f'fontsize={10*size}, fillcolor="{color}", fontcolor="black"];')

        dot_output.append('    }')

    # Add edges
    for item in json_data['data']:
        if item['parent_id'] is not None:
            dot_output.append(f'    "{item["parent_id"]}" -> "{item["id"]}";')

    dot_output.append("}")
    return "\n".join(dot_output)

def create_dot_graph(json_data):
    dot_output = ["digraph G {"]

    # Add global attributes for better readability
    dot_output.append('    node [shape=box, style="rounded,filled", color="#4285F4", fillcolor="#E1E1E1"];')
    dot_output.append('    edge [color="#4285F4"];')

    for item in json_data['data']:
        item_id = item['id']
        name = item['name_short_en']
        code = item['code']
        level = item['level']
        parent_id = item['parent_id']

        # Calculate node size based on level
        size = get_node_size(level)

        # Add node with more details and adjusted size
        node_label = f"{name}\\nID: {item_id}\\nCode: {code}\\nLevel: {level}"
        dot_output.append(f'    "{item_id}" [label="{node_label}", width={size}, height={size}, fontsize={10*size}];')

        # Add edge if there's a parent
        if parent_id is not None:
            dot_output.append(f'    "{parent_id}" -> "{item_id}";')

    dot_output.append("}")
    return "\n".join(dot_output)

def save_dot_file(dot_content, output_file):
    with open(output_file, 'w') as file:
        file.write(dot_content)



# Example usage
file_path = 'product_graph.json'  # Replace with your JSON file path
output_file = 'output3.dot'  # The DOT file to be created

parsed_data = load_and_parse_json(file_path)

if parsed_data:
    #dot_graph = create_dot_graph(parsed_data)
    dot_graph = create_clustered_dot_graph(parsed_data)
    save_dot_file(dot_graph, output_file)
    print(f"DOT graph has been saved to {output_file}")
    print("You can use Graphviz to visualize this DOT file.")
else:
    print("Failed to create DOT graph due to errors in loading or parsing the JSON file.")
