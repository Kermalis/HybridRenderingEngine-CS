#version 430 core

in vec2 TexCoords;

uniform sampler2D albedoMap;

void main(){
    //This kills perf but I have no idea of how else to alpha test the sponza leaves 
    float alpha = texture(albedoMap, TexCoords).a;
    if(alpha < 0.5){
        discard;
    }
}