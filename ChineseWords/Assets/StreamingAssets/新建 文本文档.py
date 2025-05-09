input_path = "G:\Chinese\Chinese\ChineseWords\Assets\StreamingAssets\THUOCL_chengyu.txt"
output_path = "G:\Chinese\Chinese\ChineseWords\Assets\StreamingAssets\THUOCL_chengyu1.txt"

with open(input_path, "r", encoding="utf-8") as fin, \
     open(output_path, "w", encoding="utf-8") as fout:
    for line in fin:
        line = line.strip()
        if not line:
            continue
        idiom = line.split()[0]
        fout.write(idiom + "\n")

print(f"已生成纯成语列表：{output_path}")
