// SPDX-License-Identifier: GPL-2.0-or-later
// uc_host - a plain native host that runs am4ever's DXE drivers under Unicorn against a
// simulated ACPI EC. Built as a non-CET exe so Unicorn's longjmp-based TCG can execute
// (a .NET host has CET user shadow stack on, which fail-fasts). Ports the Ec/Dxe classes
// of G:\HPBIOS\tools\emulate_modules.py. See native/README.md.
#include <stdio.h>
#include <stdint.h>
#include <string.h>
#include <stdlib.h>
#include <unicorn/unicorn.h>

#define IMAGE_BASE 0x10000000ULL
#define STACK_BASE 0x20000000ULL
#define SYS_BASE   0x30000000ULL
#define RET_ADDR   0x30008000ULL

typedef struct {
    int present;
    uint8_t regs[256];
    int touched[256];
    int have_cmd, cmd, have_addr, addr, have_obf, obf;   // EC port state machine
    uint32_t cf8, lpc44;
    long insns;
} Sim;

static uint32_t ec_inb(Sim *s, uint32_t port) {
    if (!s->present) return 0xFF;
    if (port == 0x66) return s->have_obf ? 1 : 0;        // OBF bit0; IBF bit1 always 0 (instant EC)
    if (port == 0x62) { uint32_t v = s->have_obf ? (uint32_t)s->obf : 0; s->have_obf = 0; return v; }
    return 0xFF;
}

static void ec_outb(Sim *s, uint32_t port, uint32_t v) {
    if (!s->present) return;
    if (port == 0x66) { s->cmd = v; s->have_cmd = 1; s->have_addr = 2; return; } // state='addr'
    if (port == 0x62) {
        if (s->have_addr == 2) {                          // expecting address byte
            s->addr = v; s->touched[v & 0xFF] = 1;
            if (s->cmd == 0x80) { s->obf = s->regs[v & 0xFF]; s->have_obf = 1; s->have_addr = 0; }
            else if (s->cmd == 0x81) s->have_addr = 1;     // state='data'
        } else if (s->have_addr == 1 && s->cmd == 0x81) {  // data byte of a write
            s->regs[s->addr & 0xFF] = (uint8_t)v; s->touched[s->addr & 0xFF] = 1; s->have_addr = 0;
        }
    }
}

static uint32_t hook_in(uc_engine *uc, uint32_t port, int size, void *user) {
    (void)uc; (void)size; Sim *s = user;
    if (port == 0xCFC && s->cf8 == 0x8000A344) return s->lpc44;
    if (port == 0x62 || port == 0x66) return ec_inb(s, port);
    return 0xFFFFFFFF;
}

static void hook_out(uc_engine *uc, uint32_t port, int size, uint32_t value, void *user) {
    (void)uc; (void)size; Sim *s = user;
    if (port == 0xCF8) s->cf8 = value;
    else if (port == 0xCFC && s->cf8 == 0x8000A344) s->lpc44 = value;
    else if (port == 0x62 || port == 0x66) ec_outb(s, port, value & 0xFF);
}

static void hook_code(uc_engine *uc, uint64_t a, uint32_t sz, void *user) { (void)uc; (void)a; (void)sz; ((Sim *)user)->insns++; }

static uint32_t rd32(const uint8_t *p) { return p[0] | (p[1] << 8) | (p[2] << 16) | ((uint32_t)p[3] << 24); }

static int run_ecfan(const uint8_t *pe, size_t pe_len, Sim *s, uint64_t limit) {
    if (pe_len < 0x40) { fprintf(stderr, "pe too small\n"); return -1; }
    uint32_t pe_off = rd32(pe + 0x3C);
    if ((uint64_t)pe_off + 24 + 64 > pe_len) { fprintf(stderr, "bad PE header\n"); return -1; }  // 64-bit: no wrap on a huge e_lfanew
    uint16_t nsec = pe[pe_off + 6] | (pe[pe_off + 7] << 8);
    uint16_t opt = pe[pe_off + 20] | (pe[pe_off + 21] << 8);
    uint32_t entry = rd32(pe + pe_off + 24 + 16);
    uint32_t size_img = rd32(pe + pe_off + 24 + 56);

    uc_engine *uc;
    if (uc_open(UC_ARCH_X86, UC_MODE_64, &uc) != UC_ERR_OK) { fprintf(stderr, "uc_open failed\n"); return -1; }
    uc_mem_map(uc, IMAGE_BASE, (size_img + 0xFFFF) & ~0xFFFFULL, UC_PROT_ALL);
    uc_mem_write(uc, IMAGE_BASE, pe, pe_len < 0x400 ? pe_len : 0x400);
    for (uint16_t i = 0; i < nsec; i++) {
        const uint8_t *sh = pe + pe_off + 24 + opt + 40 * i;
        if ((size_t)(sh - pe) + 40 > pe_len) break;
        uint32_t vs = rd32(sh + 8), va = rd32(sh + 12), rs = rd32(sh + 16), ro = rd32(sh + 20);
        uint32_t n = (vs && vs < rs) ? vs : rs;
        if ((size_t)ro + n <= pe_len) uc_mem_write(uc, IMAGE_BASE + va, pe + ro, n);
    }
    uc_mem_map(uc, STACK_BASE, 0x100000, UC_PROT_ALL);
    uc_mem_map(uc, SYS_BASE, 0x10000, UC_PROT_ALL);
    uint8_t hlt = 0xF4; uc_mem_write(uc, RET_ADDR, &hlt, 1);

    uc_hook h1, h2, h3;
    uc_hook_add(uc, &h1, UC_HOOK_INSN, (void *)hook_in, s, 1, 0, UC_X86_INS_IN);
    uc_hook_add(uc, &h2, UC_HOOK_INSN, (void *)hook_out, s, 1, 0, UC_X86_INS_OUT);
    uc_hook_add(uc, &h3, UC_HOOK_CODE, (void *)hook_code, s, 1, 0);

    uint64_t sp = STACK_BASE + 0xFF000, ret = RET_ADDR;
    uc_mem_write(uc, sp, &ret, 8);
    uc_reg_write(uc, UC_X86_REG_RSP, &sp);
    uint64_t rcx = 0x40000000, rdx = SYS_BASE;
    uc_reg_write(uc, UC_X86_REG_RCX, &rcx);
    uc_reg_write(uc, UC_X86_REG_RDX, &rdx);

    uc_err e = uc_emu_start(uc, IMAGE_BASE + entry, RET_ADDR, 0, limit);
    uint64_t rip = 0; uc_reg_read(uc, UC_X86_REG_RIP, &rip);
    int status = (e != UC_ERR_OK) ? 2 : (rip != RET_ADDR ? 1 : 0);
    uc_close(uc);
    return status;
}

static int selftest(void) {
    uc_engine *uc;
    if (uc_open(UC_ARCH_X86, UC_MODE_32, &uc) != UC_ERR_OK) return 2;
    uc_mem_map(uc, 0x1000, 0x1000, UC_PROT_ALL);
    uint8_t code[] = {0x41, 0x41, 0x41};
    uc_mem_write(uc, 0x1000, code, sizeof(code));
    int64_t ecx = 0; uc_reg_write(uc, UC_X86_REG_ECX, &ecx);
    uc_err e = uc_emu_start(uc, 0x1000, 0x1003, 0, 0);
    uc_reg_read(uc, UC_X86_REG_ECX, &ecx);
    uc_close(uc);
    if (e == UC_ERR_OK && ecx == 3) { printf("selftest=ok\n"); return 0; }
    printf("selftest=fail\n"); return 2;
}

int main(int argc, char **argv) {
    if (argc >= 2 && strcmp(argv[1], "selftest") == 0) return selftest();
    if (argc < 2 || strcmp(argv[1], "ecfan") != 0) { fprintf(stderr, "usage: uc_host ecfan --pe <file> [--no-ec] [--reg h=h] | selftest\n"); return 64; }

    Sim s; memset(&s, 0, sizeof(s)); s.present = 1;
    s.regs[0x7E] = 0x00; s.regs[0x80] = 0x03;             // defaults from emulate_modules.py
    const char *pe_path = NULL; uint64_t limit = 50000000;
    for (int i = 2; i < argc; i++) {
        if (strcmp(argv[i], "--pe") == 0 && i + 1 < argc) pe_path = argv[++i];
        else if (strcmp(argv[i], "--no-ec") == 0) s.present = 0;
        else if (strcmp(argv[i], "--reg") == 0 && i + 1 < argc) {
            unsigned a, v; if (sscanf(argv[++i], "%x=%x", &a, &v) == 2) s.regs[a & 0xFF] = (uint8_t)v;
        }
    }
    if (!pe_path) { fprintf(stderr, "--pe required\n"); return 64; }
    FILE *f = fopen(pe_path, "rb");
    if (!f) { fprintf(stderr, "cannot open %s\n", pe_path); return 64; }
    fseek(f, 0, SEEK_END); long n = ftell(f); fseek(f, 0, SEEK_SET);
    uint8_t *pe = malloc(n); if (fread(pe, 1, n, f) != (size_t)n) { fclose(f); return 64; } fclose(f);

    int status = run_ecfan(pe, n, &s, limit);
    free(pe);
    if (status < 0) return 1;
    printf("status=%d\n", status);
    printf("insns=%ld\n", s.insns);
    printf("lpc=0x%X\n", s.lpc44);
    for (int i = 0; i < 256; i++) if (s.touched[i]) printf("ec[0x%X]=0x%X\n", i, s.regs[i]);
    return 0;
}
